using System.Windows;
using TeleDrive.WPF.ViewModels.Wizard;

namespace TeleDrive.WPF.Views.Wizard;

public partial class WizardWindow : Window
{
    public WizardWindow()
    {
        InitializeComponent();

        if (DataContext is WizardViewModel viewModel)
        {
            viewModel.WizardCompleted += OnWizardCompleted;
        }
    }

    private async void OnWizardCompleted(object? sender, EventArgs e)
    {
        if (sender is not WizardViewModel viewModel)
        {
            return;
        }

        App.Settings.ConnectionMode = viewModel.SelectedMode;
        App.Settings.BotToken = viewModel.BotToken;
        App.Settings.ApiId = viewModel.ApiId;
        App.Settings.ApiHash = viewModel.ApiHash;
        App.Settings.PhoneNumber = viewModel.PhoneNumber;
        App.Settings.VaultChannelId = viewModel.VaultChannelId;

        await App.SaveSettingsAsync();
        App.InitializeServices();

        App.ShowMainWindow();
        Close();
    }
}
