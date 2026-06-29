using System.Security.Claims;
using ChatSystem.Core.Enums;
using ChatSystem.Server.Data;
using ChatSystem.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatSystem.Server.Pages.UserPages;

[Authorize(Roles = "User")]
public class ChatModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public ChatModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public int CurrentUserId { get; private set; }

    public List<FriendItem> Friends { get; private set; } = new();

    public FriendItem? SelectedFriend { get; private set; }

    public List<MessageItem> Messages { get; private set; } = new();

    public async Task OnGetAsync(int? friendId)
    {
        CurrentUserId = GetCurrentUserId();
        Friends = await LoadFriendsAsync(CurrentUserId);

        if (friendId is null)
        {
            SelectedFriend = Friends.FirstOrDefault();
        }
        else
        {
            SelectedFriend = Friends.SingleOrDefault(friend => friend.Id == friendId.Value);
        }

        if (SelectedFriend is not null)
        {
            Messages = await LoadMessagesAsync(CurrentUserId, SelectedFriend.Id);
        }
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
            .Where(user =>
                friendIds.Contains(user.Id) &&
                user.Role == UserRole.User &&
                user.Status == UserStatus.Active)
            .ToListAsync();

        return friendIds
            .Select(friendId => users.SingleOrDefault(user => user.Id == friendId))
            .Where(user => user is not null)
            .Select(user => new FriendItem(user!.Id, user.Username, user.DisplayName))
            .ToList();
    }

    private async Task<List<MessageItem>> LoadMessagesAsync(int currentUserId, int friendId)
    {
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
            .Select(message => new MessageItem(
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

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : 0;
    }

    public record FriendItem(int Id, string Username, string DisplayName);

    public record MessageItem(
        int Id,
        int FromUserId,
        int ToUserId,
        string Content,
        int Type,
        string? FileName,
        long? FileSize,
        string? DownloadUrl,
        DateTime SentAt);
}
