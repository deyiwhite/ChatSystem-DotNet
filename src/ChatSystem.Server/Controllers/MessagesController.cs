using System.Security.Claims;
using ChatSystem.Core.Entities;
using ChatSystem.Core.Enums;
using ChatSystem.Server.Authentication;
using ChatSystem.Server.Data;
using ChatSystem.Server.Hubs;
using ChatSystem.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatSystem.Server.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize(
    AuthenticationSchemes = DesktopTokenAuthenticationDefaults.AuthenticationScheme,
    Roles = "User")]
public class MessagesController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHubContext<ChatHub> _hubContext;

    public MessagesController(ApplicationDbContext dbContext, IHubContext<ChatHub> hubContext)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
    }

    [HttpGet("{friendId:int}")]
    public async Task<ActionResult<List<MessageResponse>>> GetMessages(int friendId)
    {
        var currentUserId = GetCurrentUserId();
        if (!await AreFriendsAsync(currentUserId, friendId))
        {
            return Forbid();
        }

        var messages = await _dbContext.Messages
            .Where(message =>
                !message.IsDeletedByAdmin &&
                ((message.FromUserId == currentUserId &&
                  message.ToUserId == friendId &&
                  !message.IsDeletedBySender) ||
                 (message.FromUserId == friendId &&
                  message.ToUserId == currentUserId &&
                  !message.IsDeletedByReceiver)))
            .OrderBy(message => message.SentAt)
            .Select(message => new
            {
                message.Id,
                message.FromUserId,
                message.ToUserId,
                message.Content,
                message.Type,
                message.AttachmentFileName,
                message.AttachmentSize,
                message.SentAt
            })
            .ToListAsync();

        return messages
            .Select(message => new MessageResponse(
                message.Id,
                message.FromUserId,
                message.ToUserId,
                message.Content,
                (int)message.Type,
                message.AttachmentFileName,
                message.AttachmentSize,
                message.Type == MessageType.File ? FileLinks.Private(message.Id) : null,
                message.SentAt))
            .ToList();
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<HistoryMessageResponse>>> GetHistoryMessages(
        [FromQuery] int? friendId,
        [FromQuery] string? keyword,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        const int maxMessages = 200;
        var currentUserId = GetCurrentUserId();

        var query = _dbContext.Messages
            .Where(message =>
                !message.IsDeletedByAdmin &&
                ((message.FromUserId == currentUserId && !message.IsDeletedBySender) ||
                 (message.ToUserId == currentUserId && !message.IsDeletedByReceiver)));

        if (friendId is not null)
        {
            if (!await AreFriendsAsync(currentUserId, friendId.Value))
            {
                return BadRequest(new ErrorResponse("好友关系不存在"));
            }

            query = query.Where(message =>
                (message.FromUserId == currentUserId && message.ToUserId == friendId.Value) ||
                (message.FromUserId == friendId.Value && message.ToUserId == currentUserId));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(message => message.Content != null && message.Content.Contains(keyword));
        }

        if (startDate is not null)
        {
            query = query.Where(message => message.SentAt >= startDate.Value.Date);
        }

        if (endDate is not null)
        {
            query = query.Where(message => message.SentAt < endDate.Value.Date.AddDays(1));
        }

        var messages = await query
            .OrderByDescending(message => message.SentAt)
            .Take(maxMessages)
            .Select(message => new
            {
                message.Id,
                message.FromUserId,
                message.ToUserId,
                message.Content,
                message.Type,
                message.AttachmentFileName,
                message.AttachmentSize,
                message.SentAt
            })
            .ToListAsync();

        var userIds = messages
            .SelectMany(message => new[] { message.FromUserId, message.ToUserId })
            .Distinct()
            .ToList();

        var users = await _dbContext.Users
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new UserInfo(user.Id, user.Username, user.DisplayName))
            .ToDictionaryAsync(user => user.Id);

        var result = messages
            .Select(message => new HistoryMessageResponse(
                message.Id,
                message.FromUserId,
                message.ToUserId,
                GetDisplayName(users, message.FromUserId),
                GetDisplayName(users, message.ToUserId),
                message.FromUserId == currentUserId,
                message.Content,
                (int)message.Type,
                message.AttachmentFileName,
                message.AttachmentSize,
                message.Type == MessageType.File ? FileLinks.Private(message.Id) : null,
                message.SentAt))
            .ToList();

        return result;
    }

    [HttpDelete("history/{id:int}")]
    public async Task<IActionResult> DeleteHistoryMessage(int id)
    {
        var currentUserId = GetCurrentUserId();
        var message = await _dbContext.Messages
            .SingleOrDefaultAsync(item =>
                item.Id == id &&
                !item.IsDeletedByAdmin &&
                ((item.FromUserId == currentUserId && !item.IsDeletedBySender) ||
                 (item.ToUserId == currentUserId && !item.IsDeletedByReceiver)));

        if (message is null)
        {
            return NotFound(new ErrorResponse("未找到可删除的历史消息"));
        }

        if (message.FromUserId == currentUserId)
        {
            message.IsDeletedBySender = true;
        }

        if (message.ToUserId == currentUserId)
        {
            message.IsDeletedByReceiver = true;
        }

        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<MessageResponse>> SendMessage(SendMessageRequest request)
    {
        var currentUserId = GetCurrentUserId();
        var content = (request.Content ?? string.Empty).Trim();

        if (request.ToUserId <= 0 || request.ToUserId == currentUserId)
        {
            return BadRequest(new ErrorResponse("接收用户不正确。"));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return BadRequest(new ErrorResponse("消息内容不能为空。"));
        }

        if (content.Length > 1000)
        {
            return BadRequest(new ErrorResponse("消息内容不能超过 1000 个字符。"));
        }

        if (!await AreFriendsAsync(currentUserId, request.ToUserId))
        {
            return Forbid();
        }

        var receiverExists = await _dbContext.Users.AnyAsync(user =>
            user.Id == request.ToUserId &&
            user.Role == UserRole.User &&
            user.Status == UserStatus.Active);

        if (!receiverExists)
        {
            return BadRequest(new ErrorResponse("接收用户不存在或当前不能接收消息。"));
        }

        var sentAt = DateTime.Now;
        var message = new Message
        {
            FromUserId = currentUserId,
            ToUserId = request.ToUserId,
            Content = content,
            Type = MessageType.Text,
            SentAt = sentAt,
            IsDeletedBySender = false,
            IsDeletedByReceiver = false,
            IsDeletedByAdmin = false
        };

        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync();

        var payload = new PrivateMessagePayload(
            message.Id,
            currentUserId,
            request.ToUserId,
            content,
            (int)MessageType.Text,
            null,
            null,
            null,
            sentAt);

        await ChatHub.BroadcastPrivateMessageAsync(_hubContext.Clients, payload);

        return new MessageResponse(
            message.Id,
            currentUserId,
            request.ToUserId,
            content,
            (int)MessageType.Text,
            null,
            null,
            null,
            sentAt);
    }

    private async Task<bool> AreFriendsAsync(int currentUserId, int friendId)
    {
        return await _dbContext.Friends.AnyAsync(friend =>
            friend.UserId == currentUserId &&
            friend.FriendId == friendId);
    }

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : 0;
    }

    private static string GetDisplayName(IReadOnlyDictionary<int, UserInfo> users, int userId)
    {
        if (!users.TryGetValue(userId, out var user))
        {
            return $"用户 {userId}";
        }

        return user.DisplayName == user.Username
            ? user.DisplayName
            : $"{user.DisplayName}（{user.Username}）";
    }
}

public sealed record SendMessageRequest(int ToUserId, string Content);

public sealed record MessageResponse(
    int Id,
    int FromUserId,
    int ToUserId,
    string Content,
    int Type,
    string? FileName,
    long? FileSize,
    string? DownloadUrl,
    DateTime SentAt);

public sealed record HistoryMessageResponse(
    int Id,
    int FromUserId,
    int ToUserId,
    string FromName,
    string ToName,
    bool IsMine,
    string Content,
    int Type,
    string? FileName,
    long? FileSize,
    string? DownloadUrl,
    DateTime SentAt);

public sealed record UserInfo(int Id, string Username, string DisplayName);
