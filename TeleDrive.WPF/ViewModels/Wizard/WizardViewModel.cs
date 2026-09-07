using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TeleDrive.Core.Models;

namespace TeleDrive.WPF.ViewModels.Wizard;

public partial class WizardViewModel : ObservableObject
{
    [ObservableProperty]
    private int _currentStepIndex;

    [ObservableProperty]
    private object? _currentPageViewModel;

    [ObservableProperty]
    private bool _canGoNext = true;

    [ObservableProperty]
    private bool _canGoBack;

    public ConnectionMode SelectedMode { get; set; }
    public string? BotToken { get; set; }
    public int ApiId { get; set; }
    public string? ApiHash { get; set; }
    public string? PhoneNumber { get; set; }
    public long VaultChannelId { get; set; }

    public event EventHandler? WizardCompleted;

    private readonly List<Func<object>> _stepFactories;

    public WizardViewModel()
    {
        _stepFactories = new List<Func<object>>
        {
            () => new WelcomePageViewModel(),
            () => new ChooseModePageViewModel(this),
            () => SelectedMode == ConnectionMode.BotApi
                ? new BotApiSetupPageViewModel(this)
                : new MtProtoSetupPageViewModel(this),
            () => new StoragePageViewModel(this),
            () => new DonePageViewModel(this)
        };

        NavigateToStep(0);
    }

    [RelayCommand]
    private void Next()
    {
        if (CurrentStepIndex >= _stepFactories.Count - 1)
        {
            WizardCompleted?.Invoke(this, EventArgs.Empty);
            return;
        }

        NavigateToStep(CurrentStepIndex + 1);
    }

    [RelayCommand]
    private void Back()
    {
        if (CurrentStepIndex > 0)
        {
            NavigateToStep(CurrentStepIndex - 1);
        }
    }

    private void NavigateToStep(int index)
    {
        CurrentStepIndex = index;
        CurrentPageViewModel = _stepFactories[index]();
        CanGoBack = index > 0;
    }
}
