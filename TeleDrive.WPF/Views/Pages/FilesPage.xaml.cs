using System.IO;
using System.Windows;
using System.Windows.Controls;
using TeleDrive.WPF.ViewModels;

namespace TeleDrive.WPF.Views.Pages;

public partial class FilesPage : UserControl
{
    public FilesPage()
    {
        InitializeComponent();
    }

    private void FilesPage_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void FilesPage_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        if (DataContext is not FilesPageViewModel viewModel)
        {
            return;
        }

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                viewModel.UploadFile(path);
            }
        }
    }
}
