using CommunityToolkit.Mvvm.ComponentModel;

namespace TeleDrive.WPF.ViewModels.Wizard;

public partial class DonePageViewModel : ObservableObject
{
    public string ModeSummary { get; }
    public string ChannelSummary { get; }

    public DonePageViewModel(WizardViewModel wizard)
    {
        ModeSummary = wizard.SelectedMode == Core.Models.ConnectionMode.BotApi
            ? "Bot API"
            : "MTProto (User Account)";

        ChannelSummary = wizard.VaultChannelId != 0
            ? $"Vault channel: {wizard.VaultChannelId}"
            : "Vault channel: not set";
    }
}
