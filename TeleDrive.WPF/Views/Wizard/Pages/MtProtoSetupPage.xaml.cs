using System.Windows.Controls;
using TeleDrive.WPF.ViewModels.Wizard;

namespace TeleDrive.WPF.Views.Wizard.Pages;

public partial class MtProtoSetupPage : UserControl
{
    public MtProtoSetupPage()
    {
        InitializeComponent();
    }

    private void PasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox && DataContext is MtProtoSetupPageViewModel viewModel)
        {
            viewModel.TwoFactorPassword = passwordBox.Password;
        }
    }
}
