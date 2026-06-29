using System.ComponentModel.DataAnnotations;
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
public class HistoryModel : PageModel
{
    private const int MaxMessages = 200;
    private readonly ApplicationDbContext _dbContext;

    public HistoryModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [BindProperty(SupportsGet = true)]
    [Display(Name = "好友")]
    public int? FriendId { get; set; }

    [BindProperty(SupportsGet = true)]
    [Display(Name = "关键词")]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true)]
    [DataType(DataType.Date)]
    [Display(Name = "开始日期")]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    [DataType(DataType.Date)]
    [Display(Name = "结束日期")]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    [Display(Name = "消息类型")]
    public string MessageType { get; set; } = "private";

    [TempData]
    public string? StatusMessage { get; set; }

    public List<FriendItem> Friends { get; private set; } = new();
    public List<GroupItem> Groups { get; private set; } = new();
    public List<MessageItem> Messages { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadPageDataAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
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
            StatusMessage = "未找到可删除的历史消息。";
            return RedirectToPage(BuildRouteValues());
        }

        if (message.FromUserId == currentUserId) message.IsDeletedBySender = true;
        if (message.ToUserId == currentUserId) message.IsDeletedByReceiver = true;

        await _dbContext.SaveChangesAsync();
        StatusMessage = "已从你的历史记录中删除该消息，对方仍可查看。";
        return RedirectToPage(BuildRouteValues());
    }

    private async Task LoadPageDataAsync()
    {
        var currentUserId = GetCurrentUserId();
        Friends = await LoadFriendsAsync(currentUserId);
        Groups = await LoadGroupsAsync(currentUserId);

        if (MessageType == "group")
        {
            await LoadGroupMessagesAsync(currentUserId);
        }
        else
        {
            await LoadPrivateMessagesAsync(currentUserId);
        }
    }

    private async Task LoadPrivateMessagesAsync(int currentUserId)
    {
        if (FriendId is not null && Friends.All(f => f.Id != FriendId.Value))
            FriendId = null;

        var keyword = Keyword?.Trim();
        Keyword = keyword;

        var query = _dbContext.Messages.Where(m =>
            !m.IsDeletedByAdmin &&
            ((m.FromUserId == currentUserId && !m.IsDeletedBySender) ||
             (m.ToUserId == currentUserId && !m.IsDeletedByReceiver)));

        if (FriendId is not null)
            query = query.Where(m =>
                (m.FromUserId == currentUserId && m.ToUserId == FriendId.Value) ||
                (m.FromUserId == FriendId.Value && m.ToUserId == currentUserId));

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(m => EF.Functions.Like(m.Content, $"%{keyword}%"));

        if (StartDate is not null)
            query = query.Where(m => m.SentAt >= StartDate.Value.Date);
        if (EndDate is not null)
            query = query.Where(m => m.SentAt < EndDate.Value.Date.AddDays(1));

        var messages = await query.OrderByDescending(m => m.SentAt).Take(MaxMessages)
            .Select(m => new { m.Id, m.FromUserId, m.ToUserId, m.Content, m.Type, m.AttachmentFileName, m.AttachmentSize, m.SentAt })
            .ToListAsync();

        var userIds = messages.SelectMany(m => new[] { m.FromUserId, m.ToUserId }).Distinct().ToList();
        var users = await _dbContext.Users.Where(u => userIds.Contains(u.Id))
            .Select(u => new UserName(u.Id, u.Username, u.DisplayName)).ToDictionaryAsync(u => u.Id);

        Messages = messages.Select(m => new MessageItem(
            m.Id, m.FromUserId, GetDisplayName(users, m.FromUserId),
            m.ToUserId, GetDisplayName(users, m.ToUserId),
            m.FromUserId == currentUserId, m.Content, (int)m.Type,
            m.AttachmentFileName, m.AttachmentSize,
            m.Type == ChatSystem.Core.Enums.MessageType.File ? FileLinks.Private(m.Id) : null,
            m.SentAt, IsGroup: false, GroupName: null, CanDelete: true)).ToList();
    }

    private async Task LoadGroupMessagesAsync(int currentUserId)
    {
        var keyword = Keyword?.Trim();
        Keyword = keyword;

        var memberGroupIds = await _dbContext.GroupMembers
            .Where(gm => gm.UserId == currentUserId)
            .Select(gm => gm.GroupId).ToListAsync();

        var query = _dbContext.GroupMessages.Where(m =>
            memberGroupIds.Contains(m.GroupId) && !m.IsDeletedByAdmin);

        if (FriendId is not null)
            query = query.Where(m => m.GroupId == FriendId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(m => EF.Functions.Like(m.Content, $"%{keyword}%"));

        if (StartDate is not null)
            query = query.Where(m => m.SentAt >= StartDate.Value.Date);
        if (EndDate is not null)
            query = query.Where(m => m.SentAt < EndDate.Value.Date.AddDays(1));

        var messages = await query.OrderByDescending(m => m.SentAt).Take(MaxMessages)
            .Select(m => new { m.Id, m.FromUserId, m.GroupId, m.Content, m.Type, m.AttachmentFileName, m.AttachmentSize, m.SentAt })
            .ToListAsync();

        var userIds = messages.Select(m => m.FromUserId).Distinct().ToList();
        var groupIds = messages.Select(m => m.GroupId).Distinct().ToList();
        var users = await _dbContext.Users.Where(u => userIds.Contains(u.Id))
            .Select(u => new UserName(u.Id, u.Username, u.DisplayName)).ToDictionaryAsync(u => u.Id);
        var groups = await _dbContext.ChatGroups.Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Name);

        Messages = messages.Select(m => new MessageItem(
            m.Id, m.FromUserId, GetDisplayName(users, m.FromUserId),
            m.GroupId, groups.TryGetValue(m.GroupId, out var gn) ? gn : $"群 {m.GroupId}",
            m.FromUserId == currentUserId, m.Content, (int)m.Type,
            m.AttachmentFileName, m.AttachmentSize,
            m.Type == ChatSystem.Core.Enums.MessageType.File ? FileLinks.Group(m.Id) : null,
            m.SentAt, IsGroup: true,
            GroupName: groups.TryGetValue(m.GroupId, out var gn2) ? gn2 : null,
            CanDelete: false)).ToList();
    }

    private async Task<List<FriendItem>> LoadFriendsAsync(int currentUserId)
    {
        var friendIds = await _dbContext.Friends
            .Where(f => f.UserId == currentUserId).OrderBy(f => f.CreatedAt)
            .Select(f => f.FriendId).ToListAsync();
        if (friendIds.Count == 0) return new();
        var users = await _dbContext.Users
            .Where(u => friendIds.Contains(u.Id) && u.Role == UserRole.User && u.Status == UserStatus.Active)
            .ToListAsync();
        return friendIds.Select(id => users.SingleOrDefault(u => u.Id == id))
            .Where(u => u is not null)
            .Select(u => new FriendItem(u!.Id, u.Username, u.DisplayName)).ToList();
    }

    private async Task<List<GroupItem>> LoadGroupsAsync(int currentUserId)
    {
        var groupIds = await _dbContext.GroupMembers
            .Where(gm => gm.UserId == currentUserId).Select(gm => gm.GroupId).ToListAsync();
        if (groupIds.Count == 0) return new();
        return await _dbContext.ChatGroups.Where(g => groupIds.Contains(g.Id))
            .Select(g => new GroupItem(g.Id, g.Name)).ToListAsync();
    }

    private object BuildRouteValues() => new
    {
        FriendId,
        Keyword,
        StartDate = StartDate?.ToString("yyyy-MM-dd"),
        EndDate = EndDate?.ToString("yyyy-MM-dd"),
        MessageType
    };

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : 0;
    }

    private static string GetDisplayName(IReadOnlyDictionary<int, UserName> users, int userId)
    {
        if (!users.TryGetValue(userId, out var user)) return $"用户 {userId}";
        return user.DisplayName == user.Username ? user.DisplayName : $"{user.DisplayName}（{user.Username}）";
    }

    public record FriendItem(int Id, string Username, string DisplayName);
    public record GroupItem(int Id, string Name);
    public record UserName(int Id, string Username, string DisplayName);
    public record MessageItem(int Id, int FromUserId, string FromName, int ToUserId, string ToName,
        bool IsMine, string Content, int Type, string? FileName, long? FileSize, string? DownloadUrl,
        DateTime SentAt, bool IsGroup, string? GroupName, bool CanDelete);
}