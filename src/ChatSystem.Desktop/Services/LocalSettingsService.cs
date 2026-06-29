using System.IO;
using System.Text.Json;

namespace ChatSystem.Desktop.Services;

public sealed class LocalSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public LocalSettingsService()
    {
        _settingsPath = Path.Combine(AppContext.BaseDirectory, "desktop-settings.json");
    }

    public async Task<DesktopSettings> LoadAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            return DesktopSettings.Default;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            return await JsonSerializer.DeserializeAsync<DesktopSettings>(stream)
                   ?? DesktopSettings.Default;
        }
        catch
        {
            return DesktopSettings.Default;
        }
    }

    public async Task SaveAsync(DesktopSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }
}

public sealed record DesktopSettings(
    string ServerUrl,
    string Username,
    string Password,
    bool RememberUser)
{
    public static DesktopSettings Default => new(
        "http://127.0.0.1:5098",
        string.Empty,
        string.Empty,
        false);
}

