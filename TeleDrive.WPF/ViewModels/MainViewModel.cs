using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TeleDrive.WPF.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public FilesPageViewModel FilesPageViewModel { get; }
    public TransfersPageViewModel TransfersPageViewModel { get; }
    public SettingsPageViewModel SettingsPageViewModel { get; }

    [ObservableProperty]
    private object? _currentPageViewModel;

    [ObservableProperty]
    private string _currentPageTitle = "Files";

    public MainViewModel()
    {
        TransfersPageViewModel = new TransfersPageViewModel();
        FilesPageViewModel = new FilesPageViewModel(TransfersPageViewModel);
        SettingsPageViewModel = new SettingsPageViewModel();
        CurrentPageViewModel = FilesPageViewModel;
    }

    [RelayCommand]
    private void ShowFiles()
    {
        CurrentPageViewModel = FilesPageViewModel;
        CurrentPageTitle = "Files";
    }

    [RelayCommand]
    private void ShowTransfers()
    {
        CurrentPageViewModel = TransfersPageViewModel;
        CurrentPageTitle = "Transfers";
    }

    [RelayCommand]
    private void ShowSettings()
    {
        CurrentPageViewModel = SettingsPageViewModel;
        CurrentPageTitle = "Settings";
    }
}
