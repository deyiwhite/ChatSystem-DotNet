using System.Security.Claims;
using ChatSystem.Core.Entities;
using ChatSystem.Core.Enums;
using ChatSystem.Server.Authentication;
using ChatSystem.Server.Data;
using ChatSystem.Server.Hubs;
using ChatSystem.Server.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatSystem.Server.Controllers;

[ApiController]
[Route("api/files")]
[Authorize(
    AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme + "," + DesktopTokenAuthenticationDefaults.AuthenticationScheme,
    Roles = "User")]
public class FilesController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly FileStorageService _fileStorage;
    private readonly IHubContext<ChatHub> _hubContext;

    public FilesController(
        ApplicationDbContext dbContext,
        FileStorageService fileStorage,
        IHubContext<ChatHub> hubContext)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _hubContext = hubContext;
    }

    [HttpPost("private")]
    [RequestSizeLimit(FileStorageService.MaxFileSize + 1024 * 1024)]
    public async Task<ActionResult<MessageResponse>> UploadPrivate([FromForm] int toUserId, [FromForm] IFormFile? file)
    {
        var currentUserId = GetCurrentUserId();

        var validationError = ValidateFile(file);
        if (validationError is not null)
        {
            return BadRequest(new ErrorResponse(validationError));
        }

        if (toUserId <= 0 || toUserId == currentUserId)
        {
            return BadRequest(new ErrorResponse("接收用户不正确。"));
        }

        var areFriends = await _dbContext.Friends.AnyAsync(friend =>
            friend.UserId == currentUserId && friend.FriendId == toUserId);

        if (!areFriends)
        {
            return BadRequest(new ErrorResponse("只能给好友发送文件。"));
        }

        var receiverExists = await _dbContext.Users.AnyAsync(user =>
            user.Id == toUserId &&
            user.Role == UserRole.User &&
            user.Status == UserStatus.Active);

        if (!receiverExists)
        {
            return BadRequest(new ErrorResponse("接收用户不存在或当前不能接收文件。"));
        }

        StoredFile stored;
        await using (var stream = file!.OpenReadStream())
        {
            stored = await _fileStorage.SaveAsync(file.FileName, file.Length, stream);
        }

        var sentAt = DateTime.Now;
        var message = new Message
        {
            FromUserId = currentUserId,
            ToUserId = toUserId,
            Content = stored.OriginalName,
            Type = MessageType.File,
            AttachmentFileName = stored.OriginalName,
            AttachmentStoredName = stored.StoredName,
            AttachmentSize = stored.Size,
            SentAt = sentAt,
            IsDeletedBySender = false,
            IsDeletedByReceiver = false,
            IsDeletedByAdmin = false
        };

        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync();

        var downloadUrl = FileLinks.Private(message.Id);
        var payload = new PrivateMessagePayload(
            message.Id,
            currentUserId,
            toUserId,
            stored.OriginalName,
            (int)MessageType.File,
            stored.OriginalName,
            stored.Size,
            downloadUrl,
            sentAt);

        await ChatHub.BroadcastPrivateMessageAsync(_hubContext.Clients, payload);

        return new MessageResponse(
            message.Id,
            currentUserId,
            toUserId,
            stored.OriginalName,
            (int)MessageType.File,
            stored.OriginalName,
            stored.Size,
            downloadUrl,
            sentAt);
    }

    [HttpPost("group")]
    [RequestSizeLimit(FileStorageService.MaxFileSize + 1024 * 1024)]
    public async Task<ActionResult<GroupMessageInfo>> UploadGroup([FromForm] int groupId, [FromForm] IFormFile? file)
    {
        var currentUserId = GetCurrentUserId();

        var validationError = ValidateFile(file);
        if (validationError is not null)
        {
            return BadRequest(new ErrorResponse(validationError));
        }

        if (groupId <= 0)
        {
            return BadRequest(new ErrorResponse("群组不正确。"));
        }

        var isMember = await _dbContext.GroupMembers.AnyAsync(member =>
            member.GroupId == groupId && member.UserId == currentUserId);

        if (!isMember)
        {
            return BadRequest(new ErrorResponse("你不是该群成员，不能发送文件。"));
        }

        StoredFile stored;
        await using (var stream = file!.OpenReadStream())
        {
            stored = await _fileStorage.SaveAsync(file.FileName, file.Length, stream);
        }

        var sentAt = DateTime.Now;
        var message = new GroupMessage
        {
            GroupId = groupId,
            FromUserId = currentUserId,
            Content = stored.OriginalName,
            Type = MessageType.File,
            AttachmentFileName = stored.OriginalName,
            AttachmentStoredName = stored.StoredName,
            AttachmentSize = stored.Size,
            SentAt = sentAt,
            IsDeletedByAdmin = false
        };

        _dbContext.GroupMessages.Add(message);
        await _dbContext.SaveChangesAsync();

        var memberIds = await _dbContext.GroupMembers
            .Where(member => member.GroupId == groupId)
            .Select(member => member.UserId)
            .ToListAsync();

        var displayName = GetCurrentDisplayName();
        var downloadUrl = FileLinks.Group(message.Id);
        var payload = new GroupMessagePayload(
            message.Id,
            groupId,
            currentUserId,
            displayName,
            stored.OriginalName,
            (int)MessageType.File,
            stored.OriginalName,
            stored.Size,
            downloadUrl,
            sentAt);

        await ChatHub.BroadcastGroupMessageAsync(_hubContext.Clients, memberIds, payload);

        return new GroupMessageInfo(
            message.Id,
            groupId,
            currentUserId,
            displayName,
            stored.OriginalName,
            (int)MessageType.File,
            stored.OriginalName,
            stored.Size,
            downloadUrl,
            sentAt);
    }

    [HttpGet("private/{messageId:int}")]
    public async Task<IActionResult> DownloadPrivate(int messageId)
    {
        var currentUserId = GetCurrentUserId();
        var message = await _dbContext.Messages.SingleOrDefaultAsync(item => item.Id == messageId);

        if (message is null || message.Type != MessageType.File || message.IsDeletedByAdmin)
        {
            return NotFound(new ErrorResponse("文件不存在或已被删除。"));
        }

        if (message.FromUserId != currentUserId && message.ToUserId != currentUserId)
        {
            return Forbid();
        }

        return BuildFileResult(message.AttachmentStoredName, message.AttachmentFileName);
    }

    [HttpGet("group/{messageId:int}")]
    public async Task<IActionResult> DownloadGroup(int messageId)
    {
        var currentUserId = GetCurrentUserId();
        var message = await _dbContext.GroupMessages.SingleOrDefaultAsync(item => item.Id == messageId);

        if (message is null || message.Type != MessageType.File || message.IsDeletedByAdmin)
        {
            return NotFound(new ErrorResponse("文件不存在或已被删除。"));
        }

        var isMember = await _dbContext.GroupMembers.AnyAsync(member =>
            member.GroupId == message.GroupId && member.UserId == currentUserId);

        if (!isMember)
        {
            return Forbid();
        }

        return BuildFileResult(message.AttachmentStoredName, message.AttachmentFileName);
    }

    private IActionResult BuildFileResult(string? storedName, string? originalName)
    {
        if (string.IsNullOrWhiteSpace(storedName))
        {
            return NotFound(new ErrorResponse("文件记录不完整。"));
        }

        var path = _fileStorage.GetFilePath(storedName);
        if (!System.IO.File.Exists(path))
        {
            return NotFound(new ErrorResponse("文件已不存在于服务器。"));
        }

        var downloadName = string.IsNullOrWhiteSpace(originalName) ? storedName : originalName;
        // 统一以二进制流下载，避免浏览器直接内联渲染带来的安全风险。
        return PhysicalFile(path, "application/octet-stream", downloadName);
    }

    private static string? ValidateFile(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return "请选择要发送的文件。";
        }

        if (file.Length > FileStorageService.MaxFileSize)
        {
            return "文件超过 20 MB 上限。";
        }

        return null;
    }

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : 0;
    }

    private string GetCurrentDisplayName()
    {
        return User.FindFirstValue("DisplayName")
               ?? User.FindFirstValue(ClaimTypes.Name)
               ?? "用户";
    }
}
