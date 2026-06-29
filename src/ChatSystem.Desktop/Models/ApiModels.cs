namespace ChatSystem.Desktop.Models;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(
    string Token,
    int UserId,
    string Username,
    string DisplayName);

public sealed record FriendItem(
    int Id,
    string Username,
    string DisplayName);

public sealed record FriendRequestItem(
    int Id,
    string FromUsername,
    string FromDisplayName,
    DateTime CreatedAt);

// 与服务端 MessageResponse / PrivateMessagePayload 字段一致：Type 0=文本, 1=文件。
public sealed record MessageItem(
    int Id,
    int FromUserId,
    int ToUserId,
    string Content,
    int Type,
    string? FileName,
    long? FileSize,
    string? DownloadUrl,
    DateTime SentAt);

public sealed record SendMessageRequest(
    int ToUserId,
    string Content);

// 群组相关，与服务端 GroupSummary / GroupMemberInfo / GroupMessageInfo 字段一致。
public sealed record GroupItem(
    int Id,
    string Name,
    int OwnerId,
    bool IsOwner,
    int MemberCount,
    DateTime CreatedAt);

public sealed record GroupMemberApiModel(
    int UserId,
    string Username,
    string DisplayName,
    bool IsOwner);

public sealed record GroupMessageItem(
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

public sealed record CreateGroupRequest(string Name, List<int> MemberIds);

public sealed record AddMemberRequest(int UserId);

public sealed record SendGroupTextRequest(string Content);

public sealed record SearchUserItem(
    int Id,
    string Username,
    string DisplayName,
    bool CanRequest,
    string ActionText);

public sealed record SendFriendRequestRequest(int ToUserId);

public sealed record HistoryMessageItem(
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

public sealed record ErrorResponse(string Message);
