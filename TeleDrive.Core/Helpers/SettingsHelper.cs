using System.Text.Json;
using TeleDrive.Core.Models;

namespace TeleDrive.Core.Helpers;

public static class SettingsHelper
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<AppSettings> LoadAsync(string settingsPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(settingsPath))
        {
            return new AppSettings();
        }

        await using var stream = File.OpenRead(settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken);
        return settings ?? new AppSettings();
    }

    public static async Task SaveAsync(AppSettings settings, string settingsPath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
    }

    public static string GetDefaultSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "TeleDrive", "settings.json");
    }
}
