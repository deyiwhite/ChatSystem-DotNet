using System.Security.Claims;
using ChatSystem.Core.Entities;
using ChatSystem.Core.Enums;
using ChatSystem.Server.Authentication;
using ChatSystem.Server.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatSystem.Server.Hubs;

[Authorize(
    AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme + "," + DesktopTokenAuthenticationDefaults.AuthenticationScheme,
    Roles = "User")]
public class ChatHub : Hub
{
    private readonly ApplicationDbContext _dbContext;

    public ChatHub(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override async Task OnConnectedAsync()
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId > 0)
        {
            // 每个用户都加入自己的私有分组，私聊和群聊消息都通过该分组推送。
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroupName(currentUserId));
        }

        await base.OnConnectedAsync();
    }

    public async Task SendPrivateMessage(int toUserId, string content)
    {
        var fromUserId = GetCurrentUserId();
        var trimmedContent = (content ?? string.Empty).Trim();

        if (fromUserId <= 0)
        {
            throw new HubException("请先登录。");
        }

        if (toUserId <= 0 || toUserId == fromUserId)
        {
            throw new HubException("接收用户不正确。");
        }

        if (string.IsNullOrWhiteSpace(trimmedContent))
        {
            throw new HubException("消息内容不能为空。");
        }

        if (trimmedContent.Length > 1000)
        {
            throw new HubException("消息内容不能超过 1000 个字符。");
        }

        var areFriends = await _dbContext.Friends.AnyAsync(friend =>
            friend.UserId == fromUserId && friend.FriendId == toUserId);

        if (!areFriends)
        {
            throw new HubException("只能给好友发送消息。");
        }

        var receiverExists = await _dbContext.Users.AnyAsync(user =>
            user.Id == toUserId &&
            user.Role == UserRole.User &&
            user.Status == UserStatus.Active);

        if (!receiverExists)
        {
            throw new HubException("接收用户不存在或当前不能接收消息。");
        }

        var sentAt = DateTime.Now;
        var message = new Message
        {
            FromUserId = fromUserId,
            ToUserId = toUserId,
            Content = trimmedContent,
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
            fromUserId,
            toUserId,
            trimmedContent,
            (int)MessageType.Text,
            null,
            null,
            null,
            sentAt);

        await BroadcastPrivateMessageAsync(Clients, payload);
    }

    public async Task SendGroupMessage(int groupId, string content)
    {
        var fromUserId = GetCurrentUserId();
        var trimmedContent = (content ?? string.Empty).Trim();

        if (fromUserId <= 0)
        {
            throw new HubException("请先登录。");
        }

        if (groupId <= 0)
        {
            throw new HubException("群组不正确。");
        }

        if (string.IsNullOrWhiteSpace(trimmedContent))
        {
            throw new HubException("消息内容不能为空。");
        }

        if (trimmedContent.Length > 1000)
        {
            throw new HubException("消息内容不能超过 1000 个字符。");
        }

        var isMember = await _dbContext.GroupMembers.AnyAsync(member =>
            member.GroupId == groupId && member.UserId == fromUserId);

        if (!isMember)
        {
            throw new HubException("你不是该群成员，不能发送消息。");
        }

        var sentAt = DateTime.Now;
        var message = new GroupMessage
        {
            GroupId = groupId,
            FromUserId = fromUserId,
            Content = trimmedContent,
            Type = MessageType.Text,
            SentAt = sentAt,
            IsDeletedByAdmin = false
        };

        _dbContext.GroupMessages.Add(message);
        await _dbContext.SaveChangesAsync();

        var memberIds = await _dbContext.GroupMembers
            .Where(member => member.GroupId == groupId)
            .Select(member => member.UserId)
            .ToListAsync();

        var payload = new GroupMessagePayload(
            message.Id,
            groupId,
            fromUserId,
            GetCurrentDisplayName(),
            trimmedContent,
            (int)MessageType.Text,
            null,
            null,
            null,
            sentAt);

        await BroadcastGroupMessageAsync(Clients, memberIds, payload);
    }

    public static async Task BroadcastPrivateMessageAsync(IHubClients<IClientProxy> clients, PrivateMessagePayload payload)
    {
        await clients.Group(GetUserGroupName(payload.ToUserId))
            .SendAsync("ReceivePrivateMessage", payload);

        if (payload.FromUserId != payload.ToUserId)
        {
            await clients.Group(GetUserGroupName(payload.FromUserId))
                .SendAsync("ReceivePrivateMessage", payload);
        }
    }

    public static async Task BroadcastGroupMessageAsync(
        IHubClients<IClientProxy> clients,
        IEnumerable<int> memberUserIds,
        GroupMessagePayload payload)
    {
        foreach (var memberId in memberUserIds.Distinct())
        {
            await clients.Group(GetUserGroupName(memberId))
                .SendAsync("ReceiveGroupMessage", payload);
        }
    }

    private int GetCurrentUserId()
    {
        var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : 0;
    }

    private string GetCurrentDisplayName()
    {
        return Context.User?.FindFirstValue("DisplayName")
               ?? Context.User?.FindFirstValue(ClaimTypes.Name)
               ?? "用户";
    }

    private static string GetUserGroupName(int userId)
    {
        return $"user-{userId}";
    }
}
