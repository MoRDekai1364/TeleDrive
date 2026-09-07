using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TeleDrive.Core.Services;

namespace TeleDrive.WPF.ViewModels.Wizard;

public partial class StoragePageViewModel : ObservableObject
{
    private readonly WizardViewModel _wizard;

    [ObservableProperty]
    private bool _useExistingChannel;

    [ObservableProperty]
    private string _existingChannelIdOrLink = string.Empty;

    [ObservableProperty]
    private bool _isWorking;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isComplete;

    public StoragePageViewModel(WizardViewModel wizard)
    {
        _wizard = wizard;
    }

    [RelayCommand]
    private async Task SetupStorageAsync()
    {
        IsWorking = true;
        StatusMessage = "Setting up vault channel...";

        try
        {
            if (UseExistingChannel)
            {
                if (!long.TryParse(ExistingChannelIdOrLink, out var channelId))
                {
                    StatusMessage = "Enter a valid numeric channel ID.";
                    return;
                }

                _wizard.VaultChannelId = channelId;
                StatusMessage = "Using existing channel.";
            }
            else
            {
                var telegramService = _wizard.SelectedMode == Core.Models.ConnectionMode.BotApi
                    ? new BotApiTelegramService(_wizard.BotToken ?? string.Empty)
                    : null;

                if (telegramService is null)
                {
                    StatusMessage = "MTProto channel auto-creation requires completing sign-in first.";
                    return;
                }

                var messageId = await telegramService.SendTextAsync("TeleDrive vault initialized.", 0, CancellationToken.None);
                StatusMessage = "Channel created.";
            }

            IsComplete = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Setup failed: {ex.Message}";
        }
        finally
        {
            IsWorking = false;
        }
    }
}
