using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TeleDrive.Core.Models;
using TeleDrive.WPF.Themes;

namespace TeleDrive.WPF.ViewModels;

public partial class SettingsPageViewModel : ObservableObject
{
    public ConnectionMode ConnectionMode => App.Settings.ConnectionMode;
    public long VaultChannelId => App.Settings.VaultChannelId;

    public AppTheme[] AvailableThemes { get; } = { AppTheme.Dark, AppTheme.Light, AppTheme.System };

    [ObservableProperty]
    private AppTheme _selectedTheme;

    [ObservableProperty]
    private int _concurrentTransferLimit;

    [ObservableProperty]
    private int _maxRetryAttempts;

    [ObservableProperty]
    private int _retryBackoffBaseMilliseconds;

    [ObservableProperty]
    private long _cacheSizeLimitBytes;

    [ObservableProperty]
    private string? _statusMessage;

    public SettingsPageViewModel()
    {
        _selectedTheme = App.Settings.Theme;
        _concurrentTransferLimit = App.Settings.ConcurrentTransferLimit;
        _maxRetryAttempts = App.Settings.MaxRetryAttempts;
        _retryBackoffBaseMilliseconds = App.Settings.RetryBackoffBaseMilliseconds;
        _cacheSizeLimitBytes = App.Settings.CacheSizeLimitBytes;
    }

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        ThemeManager.Apply(value);
        _ = SaveAsync();
    }

    partial void OnConcurrentTransferLimitChanged(int value)
    {
        App.Orchestrator?.SetWorkerCount(value);
        _ = SaveAsync();
    }

    partial void OnMaxRetryAttemptsChanged(int value)
    {
        App.Orchestrator?.SetRetryPolicy(value, RetryBackoffBaseMilliseconds);
        _ = SaveAsync();
    }

    partial void OnRetryBackoffBaseMillisecondsChanged(int value)
    {
        App.Orchestrator?.SetRetryPolicy(MaxRetryAttempts, value);
        _ = SaveAsync();
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        if (App.IndexService is null)
        {
            return;
        }

        StatusMessage = "Clearing local cache...";
        await App.IndexService.RebuildLocalCacheAsync(App.Settings.VaultChannelId, CancellationToken.None);
        StatusMessage = "Local cache rebuilt.";
    }

    private async Task SaveAsync()
    {
        App.Settings.Theme = SelectedTheme;
        App.Settings.ConcurrentTransferLimit = ConcurrentTransferLimit;
        App.Settings.MaxRetryAttempts = MaxRetryAttempts;
        App.Settings.RetryBackoffBaseMilliseconds = RetryBackoffBaseMilliseconds;
        App.Settings.CacheSizeLimitBytes = CacheSizeLimitBytes;
        await App.SaveSettingsAsync();
    }
}
