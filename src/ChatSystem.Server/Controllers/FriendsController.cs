using System.Security.Claims;
using ChatSystem.Core.Entities;
using ChatSystem.Core.Enums;
using ChatSystem.Server.Authentication;
using ChatSystem.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatSystem.Server.Controllers;

[ApiController]
[Route("api/friends")]
[Authorize(
    AuthenticationSchemes = DesktopTokenAuthenticationDefaults.AuthenticationScheme,
    Roles = "User")]
public class FriendsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public FriendsController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<FriendResponse>>> GetFriends()
    {
        var currentUserId = GetCurrentUserId();
        var friendIds = await _dbContext.Friends
            .Where(friend => friend.UserId == currentUserId)
            .OrderBy(friend => friend.CreatedAt)
            .Select(friend => friend.FriendId)
            .ToListAsync();

        if (friendIds.Count == 0)
        {
            return new List<FriendResponse>();
        }

        var users = await _dbContext.Users
            .Where(user =>
                friendIds.Contains(user.Id) &&
                user.Role == UserRole.User &&
                user.Status == UserStatus.Active)
            .ToListAsync();

        return friendIds
            .Select(friendId => users.SingleOrDefault(user => user.Id == friendId))
            .Where(user => user is not null)
            .Select(user => new FriendResponse(user!.Id, user.Username, user.DisplayName))
            .ToList();
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<UserSearchResponse>>> SearchUsers([FromQuery] string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return new List<UserSearchResponse>();
        }

        var currentUserId = GetCurrentUserId();

        // 搜索除了自己和Admin之外的所有User用户
        var users = await _dbContext.Users
            .Where(user =>
                user.Id != currentUserId &&
                user.Role == UserRole.User &&
                user.Status == UserStatus.Active &&
                (user.Username.Contains(keyword) ||
                 (user.DisplayName != null && user.DisplayName.Contains(keyword))))
            .OrderBy(user => user.Username)
            .Take(20)
            .ToListAsync();

        if (users.Count == 0)
        {
            return new List<UserSearchResponse>();
        }

        // 检查用户关系
        var userIds = users.Select(u => u.Id).ToList();

        // 检查是否已经是好友
        var friendIds = await _dbContext.Friends
            .Where(f => f.UserId == currentUserId && userIds.Contains(f.FriendId))
            .Select(f => f.FriendId)
            .ToListAsync();

        // 检查是否有待处理的好友申请
        var pendingRequests = await _dbContext.FriendRequests
            .Where(r => r.Status == FriendRequestStatus.Pending &&
                       ((r.FromUserId == currentUserId && userIds.Contains(r.ToUserId)) ||
                        (r.ToUserId == currentUserId && userIds.Contains(r.FromUserId))))
            .ToListAsync();

        var pendingUserIds = pendingRequests.Select(r =>
            r.FromUserId == currentUserId ? r.ToUserId : r.FromUserId).ToList();

        var result = users.Select(user =>
        {
            var isFriend = friendIds.Contains(user.Id);
            var hasPendingRequest = pendingUserIds.Contains(user.Id);

            var actionText = isFriend ? "已是好友" :
                            hasPendingRequest ? "申请待处理" : "可申请";

            return new UserSearchResponse(
                user.Id,
                user.Username,
                user.DisplayName ?? user.Username,
                !isFriend && !hasPendingRequest,
                actionText);
        }).ToList();

        return result;
    }

    [HttpPost("requests")]
    public async Task<IActionResult> SendFriendRequest([FromBody] SendFriendRequestRequest request)
    {
        var currentUserId = GetCurrentUserId();

        if (request.ToUserId == currentUserId)
        {
            return BadRequest(new ErrorResponse("不能添加自己为好友"));
        }

        var targetUser = await _dbContext.Users.SingleOrDefaultAsync(user =>
            user.Id == request.ToUserId &&
            user.Role == UserRole.User &&
            user.Status == UserStatus.Active);

        if (targetUser is null)
        {
            return BadRequest(new ErrorResponse("目标用户不存在或无法添加"));
        }

        // 检查是否已经是好友
        var areFriends = await _dbContext.Friends.AnyAsync(f =>
            f.UserId == currentUserId && f.FriendId == request.ToUserId);

        if (areFriends)
        {
            return BadRequest(new ErrorResponse("你们已经是好友"));
        }

        // 检查是否有待处理的申请
        var pendingRequestExists = await _dbContext.FriendRequests.AnyAsync(r =>
            r.Status == FriendRequestStatus.Pending &&
            ((r.FromUserId == currentUserId && r.ToUserId == request.ToUserId) ||
             (r.FromUserId == request.ToUserId && r.ToUserId == currentUserId)));

        if (pendingRequestExists)
        {
            return BadRequest(new ErrorResponse("双方已有待处理好友申请"));
        }

        _dbContext.FriendRequests.Add(new FriendRequest
        {
            FromUserId = currentUserId,
            ToUserId = request.ToUserId,
            Status = FriendRequestStatus.Pending,
            CreatedAt = DateTime.Now
        });

        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{friendId:int}")]
    public async Task<IActionResult> DeleteFriend(int friendId)
    {
        var currentUserId = GetCurrentUserId();

        var relationships = await _dbContext.Friends
            .Where(f =>
                (f.UserId == currentUserId && f.FriendId == friendId) ||
                (f.UserId == friendId && f.FriendId == currentUserId))
            .ToListAsync();

        if (relationships.Count == 0)
        {
            return NotFound(new ErrorResponse("好友关系不存在"));
        }

        _dbContext.Friends.RemoveRange(relationships);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("requests")]
    public async Task<ActionResult<List<FriendRequestResponse>>> GetFriendRequests()
    {
        var currentUserId = GetCurrentUserId();
        var requests = await _dbContext.FriendRequests
            .Where(request =>
                request.ToUserId == currentUserId &&
                request.Status == FriendRequestStatus.Pending)
            .OrderBy(request => request.CreatedAt)
            .ToListAsync();

        var fromUserIds = requests.Select(request => request.FromUserId).Distinct().ToList();
        var users = await _dbContext.Users
            .Where(user => fromUserIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id);

        return requests
            .Select(request =>
            {
                users.TryGetValue(request.FromUserId, out var fromUser);
                return new FriendRequestResponse(
                    request.Id,
                    fromUser?.Username ?? $"User-{request.FromUserId}",
                    fromUser?.DisplayName ?? "未知用户",
                    request.CreatedAt);
            })
            .ToList();
    }

    [HttpPost("requests/{id:int}/accept")]
    public async Task<IActionResult> AcceptFriendRequest(int id)
    {
        var currentUserId = GetCurrentUserId();
        var request = await _dbContext.FriendRequests.SingleOrDefaultAsync(item =>
            item.Id == id &&
            item.ToUserId == currentUserId &&
            item.Status == FriendRequestStatus.Pending);

        if (request is null)
        {
            return NotFound(new ErrorResponse("未找到待处理好友申请。"));
        }

        var fromUser = await _dbContext.Users.SingleOrDefaultAsync(user =>
            user.Id == request.FromUserId &&
            user.Role == UserRole.User &&
            user.Status == UserStatus.Active);

        var currentUser = await _dbContext.Users.SingleOrDefaultAsync(user =>
            user.Id == currentUserId &&
            user.Role == UserRole.User &&
            user.Status == UserStatus.Active);

        if (fromUser is null || currentUser is null)
        {
            request.Status = FriendRequestStatus.Rejected;
            request.HandledAt = DateTime.Now;
            await _dbContext.SaveChangesAsync();
            return BadRequest(new ErrorResponse("申请用户状态异常，已拒绝该申请。"));
        }

        request.Status = FriendRequestStatus.Accepted;
        request.HandledAt = DateTime.Now;

        var now = DateTime.Now;
        if (!await FriendExistsAsync(request.FromUserId, currentUserId))
        {
            _dbContext.Friends.Add(new Friend
            {
                UserId = request.FromUserId,
                FriendId = currentUserId,
                CreatedAt = now
            });
        }

        if (!await FriendExistsAsync(currentUserId, request.FromUserId))
        {
            _dbContext.Friends.Add(new Friend
            {
                UserId = currentUserId,
                FriendId = request.FromUserId,
                CreatedAt = now
            });
        }

        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("requests/{id:int}/reject")]
    public async Task<IActionResult> RejectFriendRequest(int id)
    {
        var currentUserId = GetCurrentUserId();
        var request = await _dbContext.FriendRequests.SingleOrDefaultAsync(item =>
            item.Id == id &&
            item.ToUserId == currentUserId &&
            item.Status == FriendRequestStatus.Pending);

        if (request is null)
        {
            return NotFound(new ErrorResponse("未找到待处理好友申请。"));
        }

        request.Status = FriendRequestStatus.Rejected;
        request.HandledAt = DateTime.Now;
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    private async Task<bool> FriendExistsAsync(int userId, int friendId)
    {
        return await _dbContext.Friends.AnyAsync(friend =>
            friend.UserId == userId &&
            friend.FriendId == friendId);
    }

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : 0;
    }
}

public sealed record FriendResponse(
    int Id,
    string Username,
    string DisplayName);

public sealed record FriendRequestResponse(
    int Id,
    string FromUsername,
    string FromDisplayName,
    DateTime CreatedAt);

public sealed record UserSearchResponse(
    int Id,
    string Username,
    string DisplayName,
    bool CanRequest,
    string ActionText);

public sealed record SendFriendRequestRequest(int ToUserId);
