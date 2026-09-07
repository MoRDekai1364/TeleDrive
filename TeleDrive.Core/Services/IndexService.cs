using System.Text.Json;
using Microsoft.Data.Sqlite;
using TeleDrive.Core.Interfaces;
using TeleDrive.Core.Models;

namespace TeleDrive.Core.Services;

/// <summary>
/// Reads and writes the JSON file index stored in a pinned message in the vault channel,
/// with a local SQLite mirror for instant offline browsing.
/// </summary>
public class IndexService : IIndexService
{
    private readonly ITelegramService _telegramService;
    private readonly string _cacheDbPath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public IndexService(ITelegramService telegramService, string cacheDbPath)
    {
        _telegramService = telegramService;
        _cacheDbPath = cacheDbPath;
    }

    public async Task<List<VaultFile>> ReadIndexAsync(long channelId, CancellationToken cancellationToken)
    {
        var pinned = await _telegramService.GetPinnedMessageAsync(channelId, cancellationToken);
        if (pinned is null)
        {
            return new List<VaultFile>();
        }

        var files = DeserializeIndex(pinned.Value.Text);
        await WriteToLocalCacheAsync(files, cancellationToken);
        return files;
    }

    public async Task WriteIndexAsync(List<VaultFile> files, long channelId, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var json = SerializeIndex(files);
            var pinned = await _telegramService.GetPinnedMessageAsync(channelId, cancellationToken);

            if (pinned is null)
            {
                var messageId = await _telegramService.SendTextAsync(json, channelId, cancellationToken);
                await _telegramService.PinMessageAsync(messageId, channelId, cancellationToken);
            }
            else
            {
                await _telegramService.EditMessageTextAsync(pinned.Value.MessageId, channelId, json, cancellationToken);
            }

            await WriteToLocalCacheAsync(files, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task RebuildLocalCacheAsync(long channelId, CancellationToken cancellationToken)
    {
        var files = await ReadIndexAsync(channelId, cancellationToken);
        await WriteToLocalCacheAsync(files, cancellationToken, resetTable: true);
    }

    public async Task<List<VaultFile>> ReadLocalCacheAsync()
    {
        await EnsureDatabaseAsync(CancellationToken.None);

        await using var connection = new SqliteConnection($"Data Source={_cacheDbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Data FROM FileIndex WHERE Id = 1";

        var result = await command.ExecuteScalarAsync();
        if (result is not string json)
        {
            return new List<VaultFile>();
        }

        return DeserializeIndex(json);
    }

    private async Task WriteToLocalCacheAsync(List<VaultFile> files, CancellationToken cancellationToken, bool resetTable = false)
    {
        await EnsureDatabaseAsync(cancellationToken);

        await using var connection = new SqliteConnection($"Data Source={_cacheDbPath}");
        await connection.OpenAsync(cancellationToken);

        if (resetTable)
        {
            var dropCommand = connection.CreateCommand();
            dropCommand.CommandText = "DELETE FROM FileIndex";
            await dropCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var json = SerializeIndex(files);

        var upsertCommand = connection.CreateCommand();
        upsertCommand.CommandText = """
            INSERT INTO FileIndex (Id, Data) VALUES (1, $data)
            ON CONFLICT(Id) DO UPDATE SET Data = $data
            """;
        upsertCommand.Parameters.AddWithValue("$data", json);
        await upsertCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureDatabaseAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_cacheDbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = new SqliteConnection($"Data Source={_cacheDbPath}");
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS FileIndex (
                Id INTEGER PRIMARY KEY,
                Data TEXT NOT NULL
            )
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string SerializeIndex(List<VaultFile> files)
    {
        var payload = new IndexPayload
        {
            Version = 1,
            LastUpdated = DateTimeOffset.UtcNow,
            Files = files
        };

        return JsonSerializer.Serialize(payload);
    }

    private static List<VaultFile> DeserializeIndex(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<VaultFile>();
        }

        var payload = JsonSerializer.Deserialize<IndexPayload>(json);
        return payload?.Files ?? new List<VaultFile>();
    }

    private class IndexPayload
    {
        public int Version { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
        public List<VaultFile> Files { get; set; } = new();
    }
}
