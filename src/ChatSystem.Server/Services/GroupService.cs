using ChatSystem.Core.Entities;
using ChatSystem.Core.Enums;
using ChatSystem.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ChatSystem.Server.Services;

/// <summary>
/// 群组相关业务逻辑，Web Razor 页面与桌面端 API 控制器共用，避免重复实现。
/// 校验失败时抛出 <see cref="GroupException"/>，调用方负责转成提示信息。
/// </summary>
public sealed class GroupService
{
    private const int MaxGroupMessages = 200;

    private readonly ApplicationDbContext _dbContext;

    public GroupService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<GroupSummary>> GetGroupsForUserAsync(int userId)
    {
        var groupIds = await _dbContext.GroupMembers
            .Where(member => member.UserId == userId)
            .Select(member => member.GroupId)
            .ToListAsync();

        if (groupIds.Count == 0)
        {
            return new List<GroupSummary>();
        }

        var groups = await _dbContext.ChatGroups
            .Where(group => groupIds.Contains(group.Id))
            .ToListAsync();

        var memberCounts = await _dbContext.GroupMembers
            .Where(member => groupIds.Contains(member.GroupId))
            .GroupBy(member => member.GroupId)
            .Select(grouping => new { GroupId = grouping.Key, Count = grouping.Count() })
            .ToDictionaryAsync(item => item.GroupId, item => item.Count);

        return groups
            .OrderByDescending(group => group.CreatedAt)
            .Select(group => new GroupSummary(
                group.Id,
                group.Name,
                group.OwnerId,
                group.OwnerId == userId,
                memberCounts.TryGetValue(group.Id, out var count) ? count : 0,
                group.CreatedAt))
            .ToList();
    }

    public async Task<GroupSummary> CreateGroupAsync(int ownerId, string name, IEnumerable<int> memberIds)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new GroupException("请输入群名称。");
        }

        if (trimmedName.Length > 50)
        {
            throw new GroupException("群名称不能超过 50 个字符。");
        }

        var now = DateTime.Now;
        var group = new ChatGroup
        {
            Name = trimmedName,
            OwnerId = ownerId,
            CreatedAt = now
        };

        _dbContext.ChatGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        _dbContext.GroupMembers.Add(new GroupMember
        {
            GroupId = group.Id,
            UserId = ownerId,
            JoinedAt = now
        });

        // 只允许把“自己的好友且状态正常”的用户拉进群。
        var friendIds = await _dbContext.Friends
            .Where(friend => friend.UserId == ownerId)
            .Select(friend => friend.FriendId)
            .ToListAsync();

        var validMemberIds = (memberIds ?? Enumerable.Empty<int>())
            .Distinct()
            .Where(id => id != ownerId && friendIds.Contains(id))
            .ToList();

        if (validMemberIds.Count > 0)
        {
            var activeIds = await _dbContext.Users
                .Where(user =>
                    validMemberIds.Contains(user.Id) &&
                    user.Role == UserRole.User &&
                    user.Status == UserStatus.Active)
                .Select(user => user.Id)
                .ToListAsync();

            foreach (var memberId in activeIds)
            {
                _dbContext.GroupMembers.Add(new GroupMember
                {
                    GroupId = group.Id,
                    UserId = memberId,
                    JoinedAt = now
                });
            }
        }

        await _dbContext.SaveChangesAsync();

        var memberCount = await _dbContext.GroupMembers.CountAsync(member => member.GroupId == group.Id);
        return new GroupSummary(group.Id, group.Name, ownerId, true, memberCount, now);
    }

    public async Task<List<GroupMemberInfo>> GetMembersAsync(int groupId, int requesterId)
    {
        var group = await EnsureMemberAsync(groupId, requesterId);

        var members = await _dbContext.GroupMembers
            .Where(member => member.GroupId == groupId)
            .OrderBy(member => member.JoinedAt)
            .ToListAsync();

        var userIds = members.Select(member => member.UserId).ToList();
        var users = await _dbContext.Users
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id);

        return members
            .Where(member => users.ContainsKey(member.UserId))
            .Select(member =>
            {
                var user = users[member.UserId];
                return new GroupMemberInfo(user.Id, user.Username, user.DisplayName, user.Id == group.OwnerId);
            })
            .ToList();
    }

    public async Task AddMemberAsync(int groupId, int actorId, int targetUserId)
    {
        await EnsureMemberAsync(groupId, actorId);

        if (targetUserId == actorId)
        {
            throw new GroupException("不能重复添加自己。");
        }

        var isFriend = await _dbContext.Friends.AnyAsync(friend =>
            friend.UserId == actorId && friend.FriendId == targetUserId);

        if (!isFriend)
        {
            throw new GroupException("只能邀请自己的好友加入群聊。");
        }

        var targetActive = await _dbContext.Users.AnyAsync(user =>
            user.Id == targetUserId &&
            user.Role == UserRole.User &&
            user.Status == UserStatus.Active);

        if (!targetActive)
        {
            throw new GroupException("目标用户不存在或当前不能加入群聊。");
        }

        var alreadyMember = await _dbContext.GroupMembers.AnyAsync(member =>
            member.GroupId == groupId && member.UserId == targetUserId);

        if (alreadyMember)
        {
            throw new GroupException("该用户已经在群里。");
        }

        _dbContext.GroupMembers.Add(new GroupMember
        {
            GroupId = groupId,
            UserId = targetUserId,
            JoinedAt = DateTime.Now
        });

        await _dbContext.SaveChangesAsync();
    }

    public async Task RemoveMemberAsync(int groupId, int ownerId, int targetUserId)
    {
        var group = await EnsureOwnerAsync(groupId, ownerId);

        if (targetUserId == group.OwnerId)
        {
            throw new GroupException("群主不能被移出，请使用解散群聊。");
        }

        var member = await _dbContext.GroupMembers.SingleOrDefaultAsync(item =>
            item.GroupId == groupId && item.UserId == targetUserId);

        if (member is null)
        {
            throw new GroupException("该用户不在群里。");
        }

        _dbContext.GroupMembers.Remove(member);
        await _dbContext.SaveChangesAsync();
    }

    public async Task LeaveGroupAsync(int groupId, int userId)
    {
        var group = await GetGroupAsync(groupId);

        if (group.OwnerId == userId)
        {
            throw new GroupException("群主不能退出，请使用解散群聊。");
        }

        var member = await _dbContext.GroupMembers.SingleOrDefaultAsync(item =>
            item.GroupId == groupId && item.UserId == userId);

        if (member is null)
        {
            throw new GroupException("你不在该群里。");
        }

        _dbContext.GroupMembers.Remove(member);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DisbandGroupAsync(int groupId, int ownerId)
    {
        var group = await EnsureOwnerAsync(groupId, ownerId);

        // GroupMembers 与 GroupMessages 已配置级联删除，会随群组一起移除。
        _dbContext.ChatGroups.Remove(group);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<GroupMessageInfo>> GetMessagesAsync(int groupId, int requesterId)
    {
        await EnsureMemberAsync(groupId, requesterId);

        var messages = await _dbContext.GroupMessages
            .Where(message => message.GroupId == groupId && !message.IsDeletedByAdmin)
            .OrderBy(message => message.SentAt)
            .Take(MaxGroupMessages)
            .Select(message => new
            {
                message.Id,
                message.GroupId,
                message.FromUserId,
                message.Content,
                message.Type,
                message.AttachmentFileName,
                message.AttachmentSize,
                message.SentAt
            })
            .ToListAsync();

        var userIds = messages.Select(message => message.FromUserId).Distinct().ToList();
        var users = await _dbContext.Users
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName);

        return messages
            .Select(message => new GroupMessageInfo(
                message.Id,
                message.GroupId,
                message.FromUserId,
                users.TryGetValue(message.FromUserId, out var displayName) ? displayName : $"用户 {message.FromUserId}",
                message.Content,
                (int)message.Type,
                message.AttachmentFileName,
                message.AttachmentSize,
                message.Type == MessageType.File ? FileLinks.Group(message.Id) : null,
                message.SentAt))
            .ToList();
    }

    public async Task<List<GroupMessageInfo>> GetGroupHistoryAsync(
    int userId,
    int? groupId,
    string? keyword,
    DateTime? startDate,
    DateTime? endDate)
    {
        // 取当前用户所在的所有群
        var userGroupIds = await _dbContext.GroupMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.GroupId)
            .ToListAsync();

        if (userGroupIds.Count == 0)
            return new List<GroupMessageInfo>();

        // 如果指定了群，验证用户是否在该群
        if (groupId.HasValue)
        {
            if (!userGroupIds.Contains(groupId.Value))
                throw new GroupException("你不是该群成员。");
            userGroupIds = new List<int> { groupId.Value };
        }

        var query = _dbContext.GroupMessages
            .Where(m => userGroupIds.Contains(m.GroupId) && !m.IsDeletedByAdmin);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(m => m.Content.Contains(keyword));

        if (startDate.HasValue)
            query = query.Where(m => m.SentAt >= startDate.Value.Date);

        if (endDate.HasValue)
            query = query.Where(m => m.SentAt < endDate.Value.Date.AddDays(1));

        var messages = await query
            .OrderByDescending(m => m.SentAt)
            .Take(200)
            .Select(m => new
            {
                m.Id,
                m.GroupId,
                m.FromUserId,
                m.Content,
                m.Type,
                m.AttachmentFileName,
                m.AttachmentSize,
                m.SentAt
            })
            .ToListAsync();

        var userIds = messages.Select(m => m.FromUserId).Distinct().ToList();
        var users = await _dbContext.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);

        return messages.Select(m => new GroupMessageInfo(
            m.Id, m.GroupId, m.FromUserId,
            users.TryGetValue(m.FromUserId, out var dn) ? dn : $"用户{m.FromUserId}",
            m.Content,
            (int)m.Type,
            m.AttachmentFileName,
            m.AttachmentSize,
            m.Type == MessageType.File ? FileLinks.Group(m.Id) : null,
            m.SentAt)).ToList();
    }
    public async Task<bool> IsMemberAsync(int groupId, int userId)
    {
        return await _dbContext.GroupMembers.AnyAsync(member =>
            member.GroupId == groupId && member.UserId == userId);
    }

    public async Task<(GroupMessageInfo Message, List<int> MemberIds)> AddTextMessageAsync(
        int groupId,
        int fromUserId,
        string fromDisplayName,
        string content)
    {
        await EnsureMemberAsync(groupId, fromUserId);

        var trimmed = (content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new GroupException("消息内容不能为空。");
        }

        if (trimmed.Length > 1000)
        {
            throw new GroupException("消息内容不能超过 1000 个字符。");
        }

        var sentAt = DateTime.Now;
        var message = new GroupMessage
        {
            GroupId = groupId,
            FromUserId = fromUserId,
            Content = trimmed,
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

        var info = new GroupMessageInfo(
            message.Id,
            groupId,
            fromUserId,
            fromDisplayName,
            trimmed,
            (int)MessageType.Text,
            null,
            null,
            null,
            sentAt);

        return (info, memberIds);
    }


    public async Task<ChatGroup?> FindGroupForMemberAsync(int groupId, int userId)
    {
        var isMember = await IsMemberAsync(groupId, userId);
        if (!isMember)
        {
            return null;
        }

        return await _dbContext.ChatGroups.SingleOrDefaultAsync(group => group.Id == groupId);
    }

    private async Task<ChatGroup> GetGroupAsync(int groupId)
    {
        var group = await _dbContext.ChatGroups.SingleOrDefaultAsync(item => item.Id == groupId);
        if (group is null)
        {
            throw new GroupException("群组不存在。");
        }

        return group;
    }

    private async Task<ChatGroup> EnsureMemberAsync(int groupId, int userId)
    {
        var group = await GetGroupAsync(groupId);
        var isMember = await IsMemberAsync(groupId, userId);
        if (!isMember)
        {
            throw new GroupException("你不是该群成员。");
        }

        return group;
    }

    private async Task<ChatGroup> EnsureOwnerAsync(int groupId, int ownerId)
    {
        var group = await GetGroupAsync(groupId);
        if (group.OwnerId != ownerId)
        {
            throw new GroupException("只有群主可以执行该操作。");
        }

        return group;
    }
}

public sealed class GroupException : Exception
{
    public GroupException(string message) : base(message)
    {
    }
}

public sealed record GroupSummary(
    int Id,
    string Name,
    int OwnerId,
    bool IsOwner,
    int MemberCount,
    DateTime CreatedAt);

public sealed record GroupMemberInfo(
    int UserId,
    string Username,
    string DisplayName,
    bool IsOwner);

public sealed record GroupMessageInfo(
    int Id,
    int GroupId,
    int FromUserId,
    string FromDisplayName,
    string Content,
    int Type,
    string? FileName,
    long? FileSize,
    string? DownloadUrl,
    DateTime SentAt);
