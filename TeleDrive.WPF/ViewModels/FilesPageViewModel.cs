using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TeleDrive.Core.Models;

namespace TeleDrive.WPF.ViewModels;

public partial class FilesPageViewModel : ObservableObject
{
    private readonly TransfersPageViewModel _transfers;

    public ObservableCollection<VaultFile> Files { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private VaultFile? _selectedFile;

    public FilesPageViewModel(TransfersPageViewModel transfers)
    {
        _transfers = transfers;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (App.IndexService is null)
        {
            StatusMessage = "Not connected. Finish setup first.";
            return;
        }

        IsLoading = true;
        StatusMessage = null;

        try
        {
            var cached = await App.IndexService.ReadLocalCacheAsync();
            ReplaceFiles(cached);

            await App.IndexService.RebuildLocalCacheAsync(App.Settings.VaultChannelId, CancellationToken.None);
            var refreshed = await App.IndexService.ReadLocalCacheAsync();
            ReplaceFiles(refreshed);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load files: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void UploadFiles()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Title = "Select files to upload"
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var path in dialog.FileNames)
            {
                UploadFile(path);
            }
        }
    }

    public void UploadFile(string filePath)
    {
        _transfers.StartUpload(filePath);
    }

    [RelayCommand]
    private void Download(VaultFile? file)
    {
        file ??= SelectedFile;
        if (file is null)
        {
            return;
        }

        var downloadsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        Directory.CreateDirectory(downloadsFolder);
        var destinationPath = Path.Combine(downloadsFolder, file.Name);

        _transfers.StartDownload(file, destinationPath);
    }

    [RelayCommand]
    private void CopyName(VaultFile? file)
    {
        file ??= SelectedFile;
        if (file is null)
        {
            return;
        }

        System.Windows.Clipboard.SetText(file.Name);
    }

    private void ReplaceFiles(List<VaultFile> files)
    {
        Files.Clear();
        foreach (var file in files.OrderByDescending(f => f.Uploaded))
        {
            Files.Add(file);
        }
    }
}
