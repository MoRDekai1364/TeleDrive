using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TeleDrive.Core.Helpers;
using TeleDrive.Core.Interfaces;
using TeleDrive.Core.Models;
using TeleDrive.Core.Services;
using TeleDrive.WPF.Views;
using TeleDrive.WPF.Views.Wizard;

namespace TeleDrive.WPF;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();
    public static ITelegramService? TelegramService { get; set; }
    public static IChunkingService ChunkingService { get; } = new ChunkingService();
    public static IIndexService? IndexService { get; set; }
    public static TransferOrchestrator? Orchestrator { get; set; }
    public static TrayIconManager? TrayIcon { get; set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        CrashLogger.PruneOldLogs(keepCount: 20);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var settingsPath = SettingsHelper.GetDefaultSettingsPath();
        Settings = await SettingsHelper.LoadAsync(settingsPath, default);

        Themes.ThemeManager.Apply(Settings.Theme);

        if (Settings.VaultChannelId != 0)
        {
            InitializeServices();
            ShowMainWindow();

            if (Orchestrator is not null)
            {
                _ = Orchestrator.RequeuePersistedTransfersAsync(null, default);
            }
        }
        else
        {
            var wizardWindow = new WizardWindow();
            wizardWindow.Show();
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var path = CrashLogger.LogException(e.Exception, "DispatcherUnhandledException");
        MessageBox.Show(
            $"An unexpected error occurred and was logged to:\n{path}",
            "TeleDrive",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            CrashLogger.LogException(exception, "AppDomainUnhandledException");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        CrashLogger.LogException(e.Exception, "UnobservedTaskException");
        e.SetObserved();
    }

    public static void ShowMainWindow()
    {
        var mainWindow = new MainWindow();
        Current.MainWindow = mainWindow;
        mainWindow.Show();

        TrayIcon?.Dispose();
        TrayIcon = new TrayIconManager(mainWindow);
    }

    public static void InitializeServices()
    {
        TelegramService = Settings.ConnectionMode switch
        {
            ConnectionMode.BotApi => new BotApiTelegramService(Settings.BotToken ?? string.Empty),
            ConnectionMode.MtProto => new MtProtoTelegramService(
                Settings.ApiId ?? 0,
                Settings.ApiHash ?? string.Empty,
                field => field switch
                {
                    "phone_number" => Settings.PhoneNumber,
                    _ => null
                }),
            _ => null
        };

        if (TelegramService is not null)
        {
            var cacheDbPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(SettingsHelper.GetDefaultSettingsPath()) ?? string.Empty,
                "cache.db");

            IndexService = new IndexService(TelegramService, cacheDbPath);

            var workingDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TeleDrive");
            var chunkSize = Settings.ConnectionMode == ConnectionMode.BotApi
                ? 49L * 1024 * 1024
                : 1900L * 1024 * 1024;

            Orchestrator = new TransferOrchestrator(
                TelegramService,
                ChunkingService,
                IndexService,
                Settings.VaultChannelId,
                chunkSize,
                workingDirectory,
                Settings.ConcurrentTransferLimit,
                Settings.MaxRetryAttempts,
                Settings.RetryBackoffBaseMilliseconds);
        }
    }

    public static async Task SaveSettingsAsync()
    {
        await SettingsHelper.SaveAsync(Settings, SettingsHelper.GetDefaultSettingsPath(), default);
    }
}
