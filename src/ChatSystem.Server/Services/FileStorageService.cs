using Microsoft.AspNetCore.Hosting;

namespace ChatSystem.Server.Services;

/// <summary>
/// 负责把上传的文件保存到 wwwroot/uploads 目录，并提供下载时的物理路径。
/// 文件以 GUID 命名存储，原始文件名只作为元数据保存在数据库中。
/// </summary>
public sealed class FileStorageService
{
    /// <summary>单个文件最大允许 20 MB。</summary>
    public const long MaxFileSize = 20L * 1024 * 1024;

    private readonly string _uploadRoot;

    public FileStorageService(IWebHostEnvironment environment)
    {
        var webRoot = string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath;

        _uploadRoot = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(_uploadRoot);
    }

    public async Task<StoredFile> SaveAsync(string originalFileName, long length, Stream content)
    {
        var safeOriginalName = BuildSafeOriginalName(originalFileName);
        var extension = GetSafeExtension(safeOriginalName);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_uploadRoot, storedName);

        await using (var fileStream = File.Create(fullPath))
        {
            await content.CopyToAsync(fileStream);
        }

        return new StoredFile(storedName, safeOriginalName, length);
    }

    public string GetFilePath(string storedName)
    {
        // 只取文件名部分，防止 storedName 含有路径分隔符导致目录穿越。
        var safeName = Path.GetFileName(storedName);
        return Path.Combine(_uploadRoot, safeName);
    }

    private static string BuildSafeOriginalName(string originalFileName)
    {
        var name = Path.GetFileName(originalFileName.Trim());
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "file";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name.Length > 200 ? name[^200..] : name;
    }

    private static string GetSafeExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 20)
        {
            return string.Empty;
        }

        return extension.All(ch => char.IsLetterOrDigit(ch) || ch == '.') ? extension : string.Empty;
    }
}

public sealed record StoredFile(string StoredName, string OriginalName, long Size);
