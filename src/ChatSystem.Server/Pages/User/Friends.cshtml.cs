using System.ComponentModel.DataAnnotations;
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
public class FriendsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public FriendsModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [BindProperty(SupportsGet = true)]
    [Display(Name = "搜索关键词")]
    public string? SearchKeyword { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public bool HasSearched { get; private set; }

    public List<SearchUserItem> SearchResults { get; private set; } = new();

    public List<FriendItem> Friends { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadPageDataAsync();
    }

    public async Task<IActionResult> OnPostSendRequestAsync(int id)
    {
        var currentUserId = GetCurrentUserId();
        SearchKeyword = Request.Form[nameof(SearchKeyword)].ToString();

        if (id == currentUserId)
        {
            StatusMessage = "不能添加自己为好友。";
            return RedirectToPage(new { SearchKeyword });
        }

        var targetUser = await _dbContext.Users.SingleOrDefaultAsync(user =>
            user.Id == id &&
            user.Role == UserRole.User &&
            user.Status == UserStatus.Active);

        if (targetUser is null)
        {
            StatusMessage = "目标用户不存在或当前不能添加。";
            return RedirectToPage(new { SearchKeyword });
        }

        var areFriends = await _dbContext.Friends.AnyAsync(friend =>
            friend.UserId == currentUserId && friend.FriendId == id);

        if (areFriends)
        {
            StatusMessage = "你们已经是好友，不能重复申请。";
            return RedirectToPage(new { SearchKeyword });
        }

        var pendingRequestExists = await _dbContext.FriendRequests.AnyAsync(request =>
            request.Status == FriendRequestStatus.Pending &&
            ((request.FromUserId == currentUserId && request.ToUserId == id) ||
             (request.FromUserId == id && request.ToUserId == currentUserId)));

        if (pendingRequestExists)
        {
            StatusMessage = "双方已有待处理好友申请，不能重复申请。";
            return RedirectToPage(new { SearchKeyword });
        }

        _dbContext.FriendRequests.Add(new FriendRequest
        {
            FromUserId = currentUserId,
            ToUserId = id,
            Status = FriendRequestStatus.Pending,
            CreatedAt = DateTime.Now
        });

        await _dbContext.SaveChangesAsync();

        StatusMessage = $"已向 {targetUser.DisplayName} 发送好友申请。";
        return RedirectToPage(new { SearchKeyword });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var currentUserId = GetCurrentUserId();
        var friendUser = await _dbContext.Users.SingleOrDefaultAsync(user => user.Id == id);

        var relationships = await _dbContext.Friends
            .Where(friend =>
                (friend.UserId == currentUserId && friend.FriendId == id) ||
                (friend.UserId == id && friend.FriendId == currentUserId))
            .ToListAsync();

        if (relationships.Count == 0)
        {
            StatusMessage = "未找到好友关系。";
            return RedirectToPage();
        }

        _dbContext.Friends.RemoveRange(relationships);
        await _dbContext.SaveChangesAsync();

        StatusMessage = friendUser is null
            ? "已删除好友。"
            : $"已删除好友 {friendUser.DisplayName}。";

        return RedirectToPage();
    }

    private async Task LoadPageDataAsync()
    {
        var currentUserId = GetCurrentUserId();
        Friends = await LoadFriendsAsync(currentUserId);

        var keyword = SearchKeyword?.Trim();
        HasSearched = !string.IsNullOrWhiteSpace(keyword);
        if (!HasSearched)
        {
            return;
        }

        SearchKeyword = keyword;

        var candidates = await _dbContext.Users
            .Where(user =>
                user.Id != currentUserId &&
                user.Role == UserRole.User &&
                user.Status == UserStatus.Active &&
                (EF.Functions.Like(user.Username, $"%{keyword}%") ||
                 EF.Functions.Like(user.DisplayName, $"%{keyword}%")))
            .OrderBy(user => user.Username)
            .Take(20)
            .ToListAsync();

        var candidateIds = candidates.Select(user => user.Id).ToList();
        var friendIds = await _dbContext.Friends
            .Where(friend => friend.UserId == currentUserId && candidateIds.Contains(friend.FriendId))
            .Select(friend => friend.FriendId)
            .ToListAsync();

        var pendingUserIds = await _dbContext.FriendRequests
            .Where(request =>
                request.Status == FriendRequestStatus.Pending &&
                ((request.FromUserId == currentUserId && candidateIds.Contains(request.ToUserId)) ||
                 (request.ToUserId == currentUserId && candidateIds.Contains(request.FromUserId))))
            .Select(request => request.FromUserId == currentUserId ? request.ToUserId : request.FromUserId)
            .ToListAsync();

        SearchResults = candidates
            .Select(user =>
            {
                var isFriend = friendIds.Contains(user.Id);
                var hasPendingRequest = pendingUserIds.Contains(user.Id);
                var actionText = isFriend
                    ? "已是好友"
                    : hasPendingRequest
                        ? "申请待处理"
                        : "可申请";

                return new SearchUserItem(
                    user.Id,
                    user.Username,
                    user.DisplayName,
                    !isFriend && !hasPendingRequest,
                    actionText);
            })
            .ToList();
    }

    private async Task<List<FriendItem>> LoadFriendsAsync(int currentUserId)
    {
        var friendIds = await _dbContext.Friends
            .Where(friend => friend.UserId == currentUserId)
            .OrderBy(friend => friend.CreatedAt)
            .Select(friend => friend.FriendId)
            .ToListAsync();

        if (friendIds.Count == 0)
        {
            return new List<FriendItem>();
        }

        var users = await _dbContext.Users
            .Where(user => friendIds.Contains(user.Id))
            .ToListAsync();

        return friendIds
            .Select(friendId => users.SingleOrDefault(user => user.Id == friendId))
            .Where(user => user is not null)
            .Select(user => new FriendItem(user!.Id, user.Username, user.DisplayName))
            .ToList();
    }

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : 0;
    }

    public record SearchUserItem(
        int Id,
        string Username,
        string DisplayName,
        bool CanRequest,
        string ActionText);

    public record FriendItem(int Id, string Username, string DisplayName);
}
