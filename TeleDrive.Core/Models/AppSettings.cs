namespace TeleDrive.Core.Models;

public enum AppTheme
{
    Dark,
    Light,
    System
}

public class AppSettings
{
    public ConnectionMode ConnectionMode { get; set; }
    public string? BotToken { get; set; }
    public int? ApiId { get; set; }
    public string? ApiHash { get; set; }
    public string? PhoneNumber { get; set; }
    public long VaultChannelId { get; set; }
    public int ConcurrentTransferLimit { get; set; } = 3;
    public int MaxRetryAttempts { get; set; } = 5;
    public int RetryBackoffBaseMilliseconds { get; set; } = 500;
    public long CacheSizeLimitBytes { get; set; }
    public AppTheme Theme { get; set; } = AppTheme.System;
    public string LocalCachePath { get; set; } = string.Empty;
}
