using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TeleDrive.Core.Services;

namespace TeleDrive.WPF.ViewModels.Wizard;

public partial class MtProtoSetupPageViewModel : ObservableObject
{
    private readonly WizardViewModel _wizard;
    private TaskCompletionSource<string>? _pendingInput;

    [ObservableProperty]
    private int _apiId;

    [ObservableProperty]
    private string _apiHash = string.Empty;

    [ObservableProperty]
    private string _phoneNumber = string.Empty;

    [ObservableProperty]
    private string _otpCode = string.Empty;

    [ObservableProperty]
    private string _twoFactorPassword = string.Empty;

    [ObservableProperty]
    private bool _isAwaitingOtp;

    [ObservableProperty]
    private bool _isAwaitingPassword;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool? _isValid;

    [ObservableProperty]
    private string? _statusMessage;

    public MtProtoSetupPageViewModel(WizardViewModel wizard)
    {
        _wizard = wizard;
        _apiId = wizard.ApiId;
        _apiHash = wizard.ApiHash ?? string.Empty;
        _phoneNumber = wizard.PhoneNumber ?? string.Empty;
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        IsConnecting = true;
        StatusMessage = "Connecting...";

        try
        {
            var service = new MtProtoTelegramService(ApiId, ApiHash, RequestField);
            var result = await service.TestConnectionAsync(CancellationToken.None);

            IsValid = result;
            StatusMessage = result ? "Connected." : "Could not authenticate.";

            if (result)
            {
                _wizard.ApiId = ApiId;
                _wizard.ApiHash = ApiHash;
                _wizard.PhoneNumber = PhoneNumber;
            }
        }
        catch (Exception ex)
        {
            IsValid = false;
            StatusMessage = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsConnecting = false;
            IsAwaitingOtp = false;
            IsAwaitingPassword = false;
        }
    }

    [RelayCommand]
    private void SubmitOtp()
    {
        _pendingInput?.TrySetResult(OtpCode);
        IsAwaitingOtp = false;
    }

    [RelayCommand]
    private void SubmitPassword()
    {
        _pendingInput?.TrySetResult(TwoFactorPassword);
        IsAwaitingPassword = false;
    }

    private string? RequestField(string field)
    {
        switch (field)
        {
            case "phone_number":
                return PhoneNumber;
            case "verification_code":
                return AwaitInput(() => IsAwaitingOtp = true);
            case "password":
                return AwaitInput(() => IsAwaitingPassword = true);
            default:
                return null;
        }
    }

    private string AwaitInput(Action showPrompt)
    {
        _pendingInput = new TaskCompletionSource<string>();
        showPrompt();
        return _pendingInput.Task.GetAwaiter().GetResult();
    }
}
