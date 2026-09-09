using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using TeleDrive.Core.Interfaces;
using TeleDrive.Core.Models;

namespace TeleDrive.Core.Services;

/// <summary>
/// Bounded work queue with a resizable worker pool that runs upload and download transfers,
/// reporting per-file progress and retrying failed chunks with exponential backoff.
/// </summary>
public class TransferOrchestrator : IDisposable
{
    private readonly ITelegramService _telegramService;
    private readonly IChunkingService _chunkingService;
    private readonly IIndexService _indexService;
    private readonly long _channelId;
    private readonly long _chunkSizeBytes;
    private readonly string _workingDirectory;
    private readonly string _queueStatePath;

    private int _maxRetryAttempts;
    private int _baseBackoffMilliseconds;

    private readonly Channel<Func<CancellationToken, Task>> _workQueue;
    private readonly ConcurrentDictionary<string, TransferContext> _transfers = new();
    private readonly List<(Task Task, CancellationTokenSource Cts)> _workers = new();
    private readonly object _workerLock = new();
    private readonly CancellationTokenSource _shutdownTokenSource = new();

    public TransferOrchestrator(
        ITelegramService telegramService,
        IChunkingService chunkingService,
        IIndexService indexService,
        long channelId,
        long chunkSizeBytes,
        string workingDirectory,
        int workerCount = 3,
        int maxRetryAttempts = 5,
        int baseBackoffMilliseconds = 500)
    {
        _telegramService = telegramService;
        _chunkingService = chunkingService;
        _indexService = indexService;
        _channelId = channelId;
        _chunkSizeBytes = chunkSizeBytes;
        _workingDirectory = workingDirectory;
        _queueStatePath = Path.Combine(_workingDirectory, "queue-state.json");
        _maxRetryAttempts = maxRetryAttempts;
        _baseBackoffMilliseconds = baseBackoffMilliseconds;

        _workQueue = Channel.CreateBounded<Func<CancellationToken, Task>>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        SetWorkerCount(workerCount);
    }

    public int WorkerCount
    {
        get
        {
            lock (_workerLock)
            {
                return _workers.Count;
            }
        }
    }

    public void SetWorkerCount(int desiredCount)
    {
        desiredCount = Math.Max(1, desiredCount);

        lock (_workerLock)
        {
            while (_workers.Count < desiredCount)
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownTokenSource.Token);
                var task = Task.Run(() => WorkerLoopAsync(cts.Token));
                _workers.Add((task, cts));
            }

            while (_workers.Count > desiredCount)
            {
                var last = _workers[^1];
                last.Cts.Cancel();
                _workers.RemoveAt(_workers.Count - 1);
            }
        }
    }

    public void SetRetryPolicy(int maxRetryAttempts, int baseBackoffMilliseconds)
    {
        _maxRetryAttempts = Math.Max(1, maxRetryAttempts);
        _baseBackoffMilliseconds = Math.Max(1, baseBackoffMilliseconds);
    }

    public async Task<string> EnqueueUploadAsync(string filePath, IProgress<TransferItem>? progress, CancellationToken cancellationToken)
    {
        var transferId = Guid.NewGuid().ToString();
        var fileInfo = new FileInfo(filePath);

        var item = new TransferItem
        {
            Id = transferId,
            FileName = fileInfo.Name,
            TotalBytes = fileInfo.Length,
            Direction = TransferDirection.Upload,
            Status = TransferStatus.Queued,
            LocalPath = filePath
        };

        var context = new TransferContext(item, progress);
        _transfers[transferId] = context;

        await _workQueue.Writer.WriteAsync(async token => await RunUploadAsync(context, token), cancellationToken);
        _ = PersistQueueStateAsync();

        return transferId;
    }

    public async Task EnqueueDownloadAsync(VaultFile file, string destinationPath, IProgress<TransferItem>? progress, CancellationToken cancellationToken)
    {
        var transferId = Guid.NewGuid().ToString();

        var item = new TransferItem
        {
            Id = transferId,
            FileName = file.Name,
            TotalBytes = file.Size,
            Direction = TransferDirection.Download,
            Status = TransferStatus.Queued,
            LocalPath = destinationPath,
            VaultFileId = file.Id
        };

        var context = new TransferContext(item, progress);
        _transfers[transferId] = context;

        var completionSource = new TaskCompletionSource();
        context.CompletionSource = completionSource;

        await _workQueue.Writer.WriteAsync(async token => await RunDownloadAsync(context, file, token), cancellationToken);
        _ = PersistQueueStateAsync();

        await completionSource.Task;
    }

    public Task PauseAsync(string transferId)
    {
        if (_transfers.TryGetValue(transferId, out var context))
        {
            context.Pause();
            context.Item.Status = TransferStatus.Paused;
            _ = PersistQueueStateAsync();
        }

        return Task.CompletedTask;
    }

    public Task ResumeAsync(string transferId)
    {
        if (_transfers.TryGetValue(transferId, out var context))
        {
            context.Resume();
            context.Item.Status = TransferStatus.InProgress;
            _ = PersistQueueStateAsync();
        }

        return Task.CompletedTask;
    }

    public Task CancelAsync(string transferId)
    {
        if (_transfers.TryGetValue(transferId, out var context))
        {
            context.Cancel();
            context.Item.Status = TransferStatus.Cancelled;
            _ = PersistQueueStateAsync();
        }

        return Task.CompletedTask;
    }

    public async Task RequeuePersistedTransfersAsync(IProgress<TransferItem>? progress, CancellationToken cancellationToken)
    {
        var pending = await LoadPersistedTransfersAsync();

        foreach (var record in pending)
        {
            if (record.Direction == TransferDirection.Upload
                && !string.IsNullOrEmpty(record.LocalPath)
                && File.Exists(record.LocalPath))
            {
                await EnqueueUploadAsync(record.LocalPath, progress, cancellationToken);
            }
            else if (record.Direction == TransferDirection.Download
                && !string.IsNullOrEmpty(record.VaultFileId)
                && !string.IsNullOrEmpty(record.LocalPath))
            {
                var files = await _indexService.ReadLocalCacheAsync();
                var match = files.FirstOrDefault(f => f.Id == record.VaultFileId);

                if (match is not null)
                {
                    _ = EnqueueDownloadAsync(match, record.LocalPath, progress, cancellationToken);
                }
            }
        }
    }

    private async Task<List<PendingTransferRecord>> LoadPersistedTransfersAsync()
    {
        if (!File.Exists(_queueStatePath))
        {
            return new List<PendingTransferRecord>();
        }

        try
        {
            await using var stream = File.OpenRead(_queueStatePath);
            var records = await JsonSerializer.DeserializeAsync<List<PendingTransferRecord>>(stream);
            return records ?? new List<PendingTransferRecord>();
        }
        catch
        {
            return new List<PendingTransferRecord>();
        }
    }

    private async Task PersistQueueStateAsync()
    {
        try
        {
            Directory.CreateDirectory(_workingDirectory);

            var pending = _transfers.Values
                .Where(c => c.Item.Status is TransferStatus.Queued or TransferStatus.InProgress or TransferStatus.Paused)
                .Select(c => new PendingTransferRecord
                {
                    Id = c.Item.Id,
                    Direction = c.Item.Direction,
                    FileName = c.Item.FileName,
                    LocalPath = c.Item.LocalPath,
                    VaultFileId = c.Item.VaultFileId,
                    TotalBytes = c.Item.TotalBytes
                })
                .ToList();

            await using var stream = File.Create(_queueStatePath);
            await JsonSerializer.SerializeAsync(stream, pending);
        }
        catch
        {
        }
    }

    private async Task WorkerLoopAsync(CancellationToken workerToken)
    {
        try
        {
            await foreach (var workItem in _workQueue.Reader.ReadAllAsync(workerToken))
            {
                try
                {
                    await workItem(_shutdownTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RunUploadAsync(TransferContext context, CancellationToken shutdownToken)
    {
        var item = context.Item;
        var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken, context.CancellationTokenSource.Token).Token;

        try
        {
            item.Status = TransferStatus.InProgress;
            await context.WaitIfPausedAsync(linkedToken);

            var workingDir = Path.Combine(_workingDirectory, item.Id);
            var chunks = await _chunkingService.SplitAsync(item.LocalPath!, _chunkSizeBytes, workingDir, linkedToken);
            var fileHash = await _chunkingService.ComputeHashAsync(item.LocalPath!, linkedToken);

            for (var i = 0; i < chunks.Count; i++)
            {
                await context.WaitIfPausedAsync(linkedToken);
                linkedToken.ThrowIfCancellationRequested();

                var chunk = chunks[i];
                var chunkPath = Path.Combine(workingDir, $"{chunk.Index}.chunk");

                var messageId = await ExecuteWithRetryAsync(
                    () => _telegramService.UploadFileAsync(chunkPath, _channelId, null, linkedToken),
                    linkedToken);

                chunk.MessageId = messageId;

                item.TransferredBytes += chunk.Size;
                ReportProgress(context);
            }

            var vaultFile = new VaultFile
            {
                Id = Guid.NewGuid().ToString(),
                Name = item.FileName,
                Size = item.TotalBytes,
                Mime = "application/octet-stream",
                Uploaded = DateTimeOffset.UtcNow,
                Sha256 = fileHash,
                Chunks = chunks
            };

            var existingFiles = await _indexService.ReadIndexAsync(_channelId, linkedToken);
            existingFiles.Add(vaultFile);
            await _indexService.WriteIndexAsync(existingFiles, _channelId, linkedToken);

            Directory.Delete(workingDir, recursive: true);

            item.Status = TransferStatus.Completed;
            ReportProgress(context);
            _ = PersistQueueStateAsync();
        }
        catch (OperationCanceledException)
        {
            item.Status = TransferStatus.Cancelled;
            ReportProgress(context);
            _ = PersistQueueStateAsync();
        }
        catch (Exception ex)
        {
            item.Status = TransferStatus.Failed;
            item.ErrorMessage = ex.Message;
            ReportProgress(context);
            _ = PersistQueueStateAsync();
        }
    }

    private async Task RunDownloadAsync(TransferContext context, VaultFile file, CancellationToken shutdownToken)
    {
        var item = context.Item;
        var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken, context.CancellationTokenSource.Token).Token;

        try
        {
            item.Status = TransferStatus.InProgress;
            await context.WaitIfPausedAsync(linkedToken);

            var workingDir = Path.Combine(_workingDirectory, item.Id);
            Directory.CreateDirectory(workingDir);

            var chunkFilePaths = new List<string>();

            foreach (var chunk in file.Chunks.OrderBy(c => c.Index))
            {
                await context.WaitIfPausedAsync(linkedToken);
                linkedToken.ThrowIfCancellationRequested();

                var chunkPath = Path.Combine(workingDir, $"{chunk.Index}.chunk");

                await ExecuteWithRetryAsync(
                    async () =>
                    {
                        await _telegramService.DownloadFileAsync(chunk.MessageId, _channelId, chunkPath, null, linkedToken);
                        return 0L;
                    },
                    linkedToken);

                chunkFilePaths.Add(chunkPath);

                item.TransferredBytes += chunk.Size;
                ReportProgress(context);
            }

            await _chunkingService.ReassembleAsync(file.Chunks, chunkFilePaths, item.LocalPath!, linkedToken);

            Directory.Delete(workingDir, recursive: true);

            item.Status = TransferStatus.Completed;
            ReportProgress(context);
            context.CompletionSource?.TrySetResult();
            _ = PersistQueueStateAsync();
        }
        catch (OperationCanceledException)
        {
            item.Status = TransferStatus.Cancelled;
            ReportProgress(context);
            context.CompletionSource?.TrySetCanceled();
            _ = PersistQueueStateAsync();
        }
        catch (Exception ex)
        {
            item.Status = TransferStatus.Failed;
            item.ErrorMessage = ex.Message;
            ReportProgress(context);
            context.CompletionSource?.TrySetException(ex);
            _ = PersistQueueStateAsync();
        }
    }

    private async Task<long> ExecuteWithRetryAsync(Func<Task<long>> operation, CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                attempt++;
                if (attempt >= _maxRetryAttempts)
                {
                    throw;
                }

                var delay = TimeSpan.FromMilliseconds(_baseBackoffMilliseconds * Math.Pow(2, attempt - 1));
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static void ReportProgress(TransferContext context)
    {
        context.Progress?.Report(context.Item);
    }

    public void Dispose()
    {
        _workQueue.Writer.TryComplete();
        _shutdownTokenSource.Cancel();

        lock (_workerLock)
        {
            foreach (var worker in _workers)
            {
                worker.Cts.Dispose();
            }

            _workers.Clear();
        }

        _shutdownTokenSource.Dispose();
    }

    private class TransferContext
    {
        public TransferItem Item { get; }
        public IProgress<TransferItem>? Progress { get; }
        public CancellationTokenSource CancellationTokenSource { get; } = new();
        public TaskCompletionSource? CompletionSource { get; set; }

        private readonly SemaphoreSlim _pauseGate = new(1, 1);

        public TransferContext(TransferItem item, IProgress<TransferItem>? progress)
        {
            Item = item;
            Progress = progress;
        }

        public void Pause()
        {
            if (_pauseGate.CurrentCount > 0)
            {
                _pauseGate.Wait(0);
            }
        }

        public void Resume()
        {
            if (_pauseGate.CurrentCount == 0)
            {
                _pauseGate.Release();
            }
        }

        public void Cancel()
        {
            CancellationTokenSource.Cancel();
            Resume();
        }

        public async Task WaitIfPausedAsync(CancellationToken cancellationToken)
        {
            await _pauseGate.WaitAsync(cancellationToken);
            _pauseGate.Release();
        }
    }
}
