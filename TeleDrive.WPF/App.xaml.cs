using System.Windows;
using TeleDrive.Core.Helpers;
using TeleDrive.Core.Interfaces;
using TeleDrive.Core.Models;
using TeleDrive.Core.Services;

namespace TeleDrive.WPF;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();
    public static ITelegramService? TelegramService { get; set; }
    public static IChunkingService ChunkingService { get; } = new ChunkingService();
    public static IIndexService? IndexService { get; set; }
    public static TransferOrchestrator? Orchestrator { get; set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsPath = SettingsHelper.GetDefaultSettingsPath();
        Settings = await SettingsHelper.LoadAsync(settingsPath, default);

        if (Settings.VaultChannelId != 0)
        {
            InitializeServices();
        }
    }

    public static void InitializeServices()
    {
        TelegramService = Settings.ConnectionMode == ConnectionMode.BotApi
            ? new BotApiTelegramService(Settings.BotToken ?? string.Empty)
            : null;

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
                Settings.ConcurrentTransferLimit);
        }
    }

    public static async Task SaveSettingsAsync()
    {
        await SettingsHelper.SaveAsync(Settings, SettingsHelper.GetDefaultSettingsPath(), default);
    }
}
