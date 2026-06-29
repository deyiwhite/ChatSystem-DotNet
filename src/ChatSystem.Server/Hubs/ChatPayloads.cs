namespace ChatSystem.Server.Hubs;

/// <summary>
/// SignalR 推送给客户端的私聊消息载荷。Type: 0=文本, 1=文件。
/// 文件消息时 FileName/FileSize/DownloadUrl 才有值。
/// </summary>
public sealed record PrivateMessagePayload(
    int Id,
    int FromUserId,
    int ToUserId,
    string Content,
    int Type,
    string? FileName,
    long? FileSize,
    string? DownloadUrl,
    DateTime SentAt);

/// <summary>
/// SignalR 推送给客户端的群聊消息载荷。Type: 0=文本, 1=文件。
/// </summary>
public sealed record GroupMessagePayload(
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
