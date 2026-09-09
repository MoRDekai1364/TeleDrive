using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TeleDrive.Core.Models;

namespace TeleDrive.WPF.ViewModels;

public partial class TransfersPageViewModel : ObservableObject
{
    public ObservableCollection<TransferItem> Transfers { get; } = new();

    private readonly IProgress<TransferItem> _progress;

    public TransfersPageViewModel()
    {
        _progress = new Progress<TransferItem>(OnProgress);
    }

    public void StartUpload(string filePath)
    {
        var orchestrator = App.Orchestrator;
        if (orchestrator is null)
        {
            return;
        }

        _ = orchestrator.EnqueueUploadAsync(filePath, _progress, CancellationToken.None);
    }

    public void StartDownload(VaultFile file, string destinationPath)
    {
        var orchestrator = App.Orchestrator;
        if (orchestrator is null)
        {
            return;
        }

        _ = orchestrator.EnqueueDownloadAsync(file, destinationPath, _progress, CancellationToken.None);
    }

    [RelayCommand]
    private Task Pause(TransferItem item)
    {
        return App.Orchestrator?.PauseAsync(item.Id) ?? Task.CompletedTask;
    }

    [RelayCommand]
    private Task Resume(TransferItem item)
    {
        return App.Orchestrator?.ResumeAsync(item.Id) ?? Task.CompletedTask;
    }

    [RelayCommand]
    private Task Cancel(TransferItem item)
    {
        return App.Orchestrator?.CancelAsync(item.Id) ?? Task.CompletedTask;
    }

    private void OnProgress(TransferItem item)
    {
        var existing = Transfers.FirstOrDefault(t => t.Id == item.Id);

        if (existing is null)
        {
            Transfers.Insert(0, item);
            return;
        }

        var index = Transfers.IndexOf(existing);
        Transfers[index] = item;
    }
}
