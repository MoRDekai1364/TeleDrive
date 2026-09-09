using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace TeleDrive.WPF;

public class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Window _mainWindow;

    public TrayIconManager(Window mainWindow)
    {
        _mainWindow = mainWindow;

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Show TeleDrive", null, (_, _) => RestoreWindow());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, (_, _) => Application.Current.Shutdown());

        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "TeleDrive",
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        _notifyIcon.DoubleClick += (_, _) => RestoreWindow();

        _mainWindow.StateChanged += MainWindowOnStateChanged;
        _mainWindow.Closing += MainWindowOnClosing;
    }

    private void MainWindowOnStateChanged(object? sender, EventArgs e)
    {
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.Hide();
            _notifyIcon.ShowBalloonTip(1500, "TeleDrive", "Still running in the background.", ToolTipIcon.Info);
        }
    }

    private void MainWindowOnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _mainWindow.StateChanged -= MainWindowOnStateChanged;
        _mainWindow.Closing -= MainWindowOnClosing;
        Dispose();
    }

    private void RestoreWindow()
    {
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
