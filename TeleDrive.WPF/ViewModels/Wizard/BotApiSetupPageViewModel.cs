using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TeleDrive.Core.Services;

namespace TeleDrive.WPF.ViewModels.Wizard;

public partial class BotApiSetupPageViewModel : ObservableObject
{
    private readonly WizardViewModel _wizard;

    [ObservableProperty]
    private string _botToken = string.Empty;

    [ObservableProperty]
    private bool _isValidating;

    [ObservableProperty]
    private bool? _isValid;

    [ObservableProperty]
    private string? _validationMessage;

    public BotApiSetupPageViewModel(WizardViewModel wizard)
    {
        _wizard = wizard;
        _botToken = wizard.BotToken ?? string.Empty;
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(BotToken))
        {
            IsValid = false;
            ValidationMessage = "Enter a bot token first.";
            return;
        }

        IsValidating = true;
        ValidationMessage = null;

        try
        {
            var service = new BotApiTelegramService(BotToken);
            var result = await service.TestConnectionAsync(CancellationToken.None);

            IsValid = result;
            ValidationMessage = result ? "Connection successful." : "Could not authenticate with this token.";

            if (result)
            {
                _wizard.BotToken = BotToken;
            }
        }
        catch (Exception ex)
        {
            IsValid = false;
            ValidationMessage = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsValidating = false;
        }
    }
}
