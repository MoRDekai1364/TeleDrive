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
                IsComplete = true;
            }
            else if (_wizard.SelectedMode == Core.Models.ConnectionMode.BotApi)
            {
                StatusMessage = "Bot API cannot create Telegram channels. " +
                    "Pre-create a channel, add the bot as admin, then switch to \"Use existing channel\" and enter its ID.";
            }
            else
            {
                var mtProtoService = new MtProtoTelegramService(
                    _wizard.ApiId,
                    _wizard.ApiHash ?? string.Empty,
                    field => field == "phone_number" ? _wizard.PhoneNumber : null);

                var channelId = await mtProtoService.CreateChannelAsync(
                    "TeleDrive Vault",
                    "TeleDrive vault initialized.",
                    CancellationToken.None);

                _wizard.VaultChannelId = channelId;
                StatusMessage = "Channel created.";
                IsComplete = true;
            }
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
