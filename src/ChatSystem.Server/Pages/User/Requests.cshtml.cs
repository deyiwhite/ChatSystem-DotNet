using System.Security.Claims;
using ChatSystem.Core.Entities;
using ChatSystem.Core.Enums;
using ChatSystem.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatSystem.Server.Pages.UserPages;

[Authorize(Roles = "User")]
public class RequestsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public RequestsModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [TempData]
    public string? StatusMessage { get; set; }

    public List<RequestItem> Requests { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadRequestsAsync();
    }

    public async Task<IActionResult> OnPostAcceptAsync(int id)
    {
        var currentUserId = GetCurrentUserId();
        var request = await _dbContext.FriendRequests.SingleOrDefaultAsync(item =>
            item.Id == id &&
            item.ToUserId == currentUserId &&
            item.Status == FriendRequestStatus.Pending);

        if (request is null)
        {
            StatusMessage = "未找到待处理好友申请。";
            return RedirectToPage();
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

            StatusMessage = "申请用户状态异常，已拒绝该申请。";
            return RedirectToPage();
        }

        request.Status = FriendRequestStatus.Accepted;
        request.HandledAt = DateTime.Now;

        var now = DateTime.Now;
        var fromToCurrentExists = await _dbContext.Friends.AnyAsync(friend =>
            friend.UserId == request.FromUserId && friend.FriendId == currentUserId);
        var currentToFromExists = await _dbContext.Friends.AnyAsync(friend =>
            friend.UserId == currentUserId && friend.FriendId == request.FromUserId);

        if (!fromToCurrentExists)
        {
            _dbContext.Friends.Add(new Friend
            {
                UserId = request.FromUserId,
                FriendId = currentUserId,
                CreatedAt = now
            });
        }

        if (!currentToFromExists)
        {
            _dbContext.Friends.Add(new Friend
            {
                UserId = currentUserId,
                FriendId = request.FromUserId,
                CreatedAt = now
            });
        }

        await _dbContext.SaveChangesAsync();

        StatusMessage = $"已同意 {fromUser.DisplayName} 的好友申请。";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        var currentUserId = GetCurrentUserId();
        var request = await _dbContext.FriendRequests.SingleOrDefaultAsync(item =>
            item.Id == id &&
            item.ToUserId == currentUserId &&
            item.Status == FriendRequestStatus.Pending);

        if (request is null)
        {
            StatusMessage = "未找到待处理好友申请。";
            return RedirectToPage();
        }

        var fromUser = await _dbContext.Users.SingleOrDefaultAsync(user => user.Id == request.FromUserId);

        request.Status = FriendRequestStatus.Rejected;
        request.HandledAt = DateTime.Now;
        await _dbContext.SaveChangesAsync();

        StatusMessage = fromUser is null
            ? "已拒绝好友申请。"
            : $"已拒绝 {fromUser.DisplayName} 的好友申请。";

        return RedirectToPage();
    }

    private async Task LoadRequestsAsync()
    {
        var currentUserId = GetCurrentUserId();
        var pendingRequests = await _dbContext.FriendRequests
            .Where(request => request.ToUserId == currentUserId && request.Status == FriendRequestStatus.Pending)
            .OrderBy(request => request.CreatedAt)
            .ToListAsync();

        var fromUserIds = pendingRequests.Select(request => request.FromUserId).Distinct().ToList();
        var users = await _dbContext.Users
            .Where(user => fromUserIds.Contains(user.Id))
            .ToListAsync();

        Requests = pendingRequests
            .Select(request =>
            {
                var fromUser = users.SingleOrDefault(user => user.Id == request.FromUserId);
                return new RequestItem(
                    request.Id,
                    fromUser?.Username ?? $"User-{request.FromUserId}",
                    fromUser?.DisplayName ?? "未知用户",
                    request.CreatedAt);
            })
            .ToList();
    }

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : 0;
    }

    public record RequestItem(
        int Id,
        string FromUsername,
        string FromDisplayName,
        DateTime CreatedAt);
}
