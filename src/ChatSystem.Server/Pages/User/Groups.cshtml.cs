using System.Security.Claims;
using ChatSystem.Core.Enums;
using ChatSystem.Server.Data;
using ChatSystem.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatSystem.Server.Pages.UserPages;

[Authorize(Roles = "User")]
public class GroupsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly GroupService _groupService;

    public GroupsModel(ApplicationDbContext dbContext, GroupService groupService)
    {
        _dbContext = dbContext;
        _groupService = groupService;
    }

    [BindProperty]
    public string NewGroupName { get; set; } = string.Empty;

    [BindProperty]
    public List<int> MemberIds { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public List<FriendOption> Friends { get; private set; } = new();

    public List<GroupCard> Groups { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadPageDataAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        try
        {
            var group = await _groupService.CreateGroupAsync(GetCurrentUserId(), NewGroupName, MemberIds);
            StatusMessage = $"已创建群聊「{group.Name}」。";
        }
        catch (GroupException ex)
        {
            StatusMessage = ex.Message;
        }

        return RedirectToPage();
    }

    
    public async Task<IActionResult> OnPostAddMemberAsync(int groupId, int userId)
    {
        if (userId <= 0)
        {
            StatusMessage = "请选择要添加的成员。";
            return RedirectToPage();
        }
        try
        {
            await _groupService.AddMemberAsync(groupId, GetCurrentUserId(), userId);
            StatusMessage = "已添加群成员。";
        }
        catch (GroupException ex)
        {
            StatusMessage = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveMemberAsync(int groupId, int userId)
    {
        try
        {
            await _groupService.RemoveMemberAsync(groupId, GetCurrentUserId(), userId);
            StatusMessage = "已移出群成员。";
        }
        catch (GroupException ex)
        {
            StatusMessage = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLeaveAsync(int groupId)
    {
        try
        {
            await _groupService.LeaveGroupAsync(groupId, GetCurrentUserId());
            StatusMessage = "已退出群聊。";
        }
        catch (GroupException ex)
        {
            StatusMessage = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDisbandAsync(int groupId)
    {
        try
        {
            await _groupService.DisbandGroupAsync(groupId, GetCurrentUserId());
            StatusMessage = "已解散群聊。";
        }
        catch (GroupException ex)
        {
            StatusMessage = ex.Message;
        }

        return RedirectToPage();
    }

    private async Task LoadPageDataAsync()
    {
        var currentUserId = GetCurrentUserId();
        Friends = await LoadFriendsAsync(currentUserId);

        var summaries = await _groupService.GetGroupsForUserAsync(currentUserId);
        var cards = new List<GroupCard>();

        foreach (var summary in summaries)
        {
            var members = await _groupService.GetMembersAsync(summary.Id, currentUserId);
            var memberIds = members.Select(member => member.UserId).ToHashSet();
            var addableFriends = Friends
                .Where(friend => !memberIds.Contains(friend.Id))
                .ToList();

            cards.Add(new GroupCard(summary, members, addableFriends));
        }

        Groups = cards;
    }

    private async Task<List<FriendOption>> LoadFriendsAsync(int currentUserId)
    {
        var friendIds = await _dbContext.Friends
            .Where(friend => friend.UserId == currentUserId)
            .OrderBy(friend => friend.CreatedAt)
            .Select(friend => friend.FriendId)
            .ToListAsync();

        if (friendIds.Count == 0)
        {
            return new List<FriendOption>();
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
            .Select(user => new FriendOption(user!.Id, user.Username, user.DisplayName))
            .ToList();
    }

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : 0;
    }

    public record FriendOption(int Id, string Username, string DisplayName);

    public record GroupCard(
        GroupSummary Summary,
        List<GroupMemberInfo> Members,
        List<FriendOption> AddableFriends);
}
