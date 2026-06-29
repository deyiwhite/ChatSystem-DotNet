namespace ChatSystem.Server.Services;

/// <summary>
/// 统一构造文件下载链接，供控制器和页面复用。
/// </summary>
public static class FileLinks
{
    public static string Private(int messageId) => $"/api/files/private/{messageId}";

    public static string Group(int messageId) => $"/api/files/group/{messageId}";
}
