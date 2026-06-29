using System.ComponentModel.DataAnnotations;
using ChatSystem.Core.Enums;
using ChatSystem.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatSystem.Server.Pages.Admin;

[Authorize(Roles = "Admin")]
public class MessagesModel : PageModel
{
    private const int MaxMessages = 300;
    private readonly ApplicationDbContext _dbContext;

    public MessagesModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [BindProperty(SupportsGet = true)]
    [Display(Name = "参与用户")]
    public int? UserId { get; set; }

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
    [Display(Name = "包含管理员已删除")]
    public bool IncludeDeleted { get; set; }

    [BindProperty(SupportsGet = true)]
    [Display(Name = "消息类型")]
    public string MessageType { get; set; } = "private"; // private / group

    [TempData]
    public string? StatusMessage { get; set; }

    public List<UserOption> Users { get; private set; } = new();
    public List<MessageItem> Messages { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadPageDataAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (MessageType == "group")
        {
            var msg = await _dbContext.GroupMessages.SingleOrDefaultAsync(m => m.Id == id);
            if (msg is null) { StatusMessage = "未找到指定消息。"; return RedirectToPage(BuildRouteValues()); }
            if (msg.IsDeletedByAdmin) { StatusMessage = "该消息已被管理员删除。"; return RedirectToPage(BuildRouteValues()); }
            msg.IsDeletedByAdmin = true;
            await _dbContext.SaveChangesAsync();
            StatusMessage = "已删除该群聊消息，群成员均无法查看。";
        }
        else
        {
            var msg = await _dbContext.Messages.SingleOrDefaultAsync(m => m.Id == id);
            if (msg is null) { StatusMessage = "未找到指定消息。"; return RedirectToPage(BuildRouteValues()); }
            if (msg.IsDeletedByAdmin) { StatusMessage = "该消息已被管理员删除。"; return RedirectToPage(BuildRouteValues()); }
            msg.IsDeletedByAdmin = true;
            await _dbContext.SaveChangesAsync();
            StatusMessage = "已删除该消息，发送方和接收方都将无法查看。";
        }
        return RedirectToPage(BuildRouteValues());
    }

    private async Task LoadPageDataAsync()
    {
        Users = await _dbContext.Users
            .Where(u => u.Role == UserRole.User)
            .OrderBy(u => u.Username)
            .Select(u => new UserOption(u.Id, u.Username, u.DisplayName, u.Status))
            .ToListAsync();

        if (UserId is not null && Users.All(u => u.Id != UserId.Value))
            UserId = null;

        var keyword = Keyword?.Trim();
        Keyword = keyword;

        if (MessageType == "group")
        {
            await LoadGroupMessagesAsync(keyword);
        }
        else
        {
            await LoadPrivateMessagesAsync(keyword);
        }
    }

    private async Task LoadPrivateMessagesAsync(string? keyword)
    {
        var query = _dbContext.Messages.AsQueryable();

        if (!IncludeDeleted)
            query = query.Where(m => !m.IsDeletedByAdmin);
        if (UserId is not null)
            query = query.Where(m => m.FromUserId == UserId.Value || m.ToUserId == UserId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var matchIds = await _dbContext.Users
                .Where(u => EF.Functions.Like(u.Username, $"%{keyword}%") || EF.Functions.Like(u.DisplayName, $"%{keyword}%"))
                .Select(u => u.Id).ToListAsync();
            query = query.Where(m => EF.Functions.Like(m.Content, $"%{keyword}%") || matchIds.Contains(m.FromUserId) || matchIds.Contains(m.ToUserId));
        }
        if (StartDate is not null)
            query = query.Where(m => m.SentAt >= StartDate.Value.Date);
        if (EndDate is not null)
            query = query.Where(m => m.SentAt < EndDate.Value.Date.AddDays(1));

        var messages = await query
            .OrderByDescending(m => m.SentAt)
            .Take(MaxMessages)
            .Select(m => new { m.Id, m.FromUserId, m.ToUserId, m.Content, m.SentAt, m.IsDeletedBySender, m.IsDeletedByReceiver, m.IsDeletedByAdmin })
            .ToListAsync();

        var userIds = messages.SelectMany(m => new[] { m.FromUserId, m.ToUserId }).Distinct().ToList();
        var users = await _dbContext.Users.Where(u => userIds.Contains(u.Id))
            .Select(u => new UserName(u.Id, u.Username, u.DisplayName))
            .ToDictionaryAsync(u => u.Id);

        Messages = messages.Select(m => new MessageItem(
            m.Id, GetDisplayName(users, m.FromUserId), GetDisplayName(users, m.ToUserId),
            m.Content, m.SentAt, m.IsDeletedBySender, m.IsDeletedByReceiver, m.IsDeletedByAdmin,
            IsGroup: false, GroupName: null)).ToList();
    }

    private async Task LoadGroupMessagesAsync(string? keyword)
    {
        var query = _dbContext.GroupMessages.AsQueryable();

        if (!IncludeDeleted)
            query = query.Where(m => !m.IsDeletedByAdmin);
        if (UserId is not null)
            query = query.Where(m => m.FromUserId == UserId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var matchIds = await _dbContext.Users
                .Where(u => EF.Functions.Like(u.Username, $"%{keyword}%") || EF.Functions.Like(u.DisplayName, $"%{keyword}%"))
                .Select(u => u.Id).ToListAsync();
            var matchGroupIds = await _dbContext.ChatGroups
                .Where(g => EF.Functions.Like(g.Name, $"%{keyword}%"))
                .Select(g => g.Id).ToListAsync();
            query = query.Where(m => EF.Functions.Like(m.Content, $"%{keyword}%") || matchIds.Contains(m.FromUserId) || matchGroupIds.Contains(m.GroupId));
        }
        if (StartDate is not null)
            query = query.Where(m => m.SentAt >= StartDate.Value.Date);
        if (EndDate is not null)
            query = query.Where(m => m.SentAt < EndDate.Value.Date.AddDays(1));

        var messages = await query
            .OrderByDescending(m => m.SentAt)
            .Take(MaxMessages)
            .Select(m => new { m.Id, m.FromUserId, m.GroupId, m.Content, m.SentAt, m.IsDeletedByAdmin })
            .ToListAsync();

        var userIds = messages.Select(m => m.FromUserId).Distinct().ToList();
        var groupIds = messages.Select(m => m.GroupId).Distinct().ToList();
        var users = await _dbContext.Users.Where(u => userIds.Contains(u.Id))
            .Select(u => new UserName(u.Id, u.Username, u.DisplayName)).ToDictionaryAsync(u => u.Id);
        var groups = await _dbContext.ChatGroups.Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Name);

        Messages = messages.Select(m => new MessageItem(
            m.Id, GetDisplayName(users, m.FromUserId),
            groups.TryGetValue(m.GroupId, out var gn) ? gn : $"群 {m.GroupId}",
            m.Content, m.SentAt,
            false, false, m.IsDeletedByAdmin,
            IsGroup: true, GroupName: groups.TryGetValue(m.GroupId, out var gn2) ? gn2 : null)).ToList();
    }

    private object BuildRouteValues() => new { UserId, Keyword, StartDate = StartDate?.ToString("yyyy-MM-dd"), EndDate = EndDate?.ToString("yyyy-MM-dd"), IncludeDeleted, MessageType };

    private static string GetDisplayName(IReadOnlyDictionary<int, UserName> users, int userId)
    {
        if (!users.TryGetValue(userId, out var user)) return $"用户 {userId}";
        return user.DisplayName == user.Username ? user.DisplayName : $"{user.DisplayName}（{user.Username}）";
    }

    public record UserOption(int Id, string Username, string DisplayName, UserStatus Status);
    public record UserName(int Id, string Username, string DisplayName);
    public record MessageItem(int Id, string FromName, string ToName, string Content, DateTime SentAt,
        bool IsDeletedBySender, bool IsDeletedByReceiver, bool IsDeletedByAdmin, bool IsGroup, string? GroupName);
}