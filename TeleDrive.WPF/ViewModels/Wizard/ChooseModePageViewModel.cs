using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TeleDrive.Core.Models;

namespace TeleDrive.WPF.ViewModels.Wizard;

public partial class ChooseModePageViewModel : ObservableObject
{
    private readonly WizardViewModel _wizard;

    [ObservableProperty]
    private ConnectionMode _selectedMode = ConnectionMode.BotApi;

    public ChooseModePageViewModel(WizardViewModel wizard)
    {
        _wizard = wizard;
        _selectedMode = wizard.SelectedMode;
    }

    [RelayCommand]
    private void SelectBotApi()
    {
        SelectedMode = ConnectionMode.BotApi;
        _wizard.SelectedMode = ConnectionMode.BotApi;
    }

    [RelayCommand]
    private void SelectMtProto()
    {
        SelectedMode = ConnectionMode.MtProto;
        _wizard.SelectedMode = ConnectionMode.MtProto;
    }
}
