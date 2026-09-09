using CommunityToolkit.Mvvm.ComponentModel;

namespace TeleDrive.WPF.ViewModels.Wizard;

public partial class WelcomePageViewModel : ObservableObject
{
    public string Title { get; } = "Welcome to TeleDrive";
    public string Tagline { get; } = "Your personal cloud, powered by Telegram.";
}
