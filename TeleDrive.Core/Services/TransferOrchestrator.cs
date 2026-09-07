using System.Collections.Concurrent;
using System.Threading.Channels;
using TeleDrive.Core.Interfaces;
using TeleDrive.Core.Models;

namespace TeleDrive.Core.Services;

/// <summary>
/// Bounded work queue with a configurable worker pool that runs upload and download transfers,
/// reporting per-file progress and retrying failed chunks with exponential backoff.
/// </summary>
public class TransferOrchestrator : IDisposable
{
    private const int MaxRetryAttempts = 5;
    private const int BaseBackoffMilliseconds = 500;

    private readonly ITelegramService _telegramService;
    private readonly IChunkingService _chunkingService;
    private readonly IIndexService _indexService;
    private readonly long _channelId;
    private readonly long _chunkSizeBytes;
    private readonly string _workingDirectory;

    private readonly Channel<Func<CancellationToken, Task>> _workQueue;
    private readonly ConcurrentDictionary<string, TransferContext> _transfers = new();
    private readonly List<Task> _workers = new();
    private readonly CancellationTokenSource _shutdownTokenSource = new();

    public TransferOrchestrator(
        ITelegramService telegramService,
        IChunkingService chunkingService,
        IIndexService indexService,
        long channelId,
        long chunkSizeBytes,
        string workingDirectory,
        int workerCount = 3)
    {
        _telegramService = telegramService;
        _chunkingService = chunkingService;
        _indexService = indexService;
        _channelId = channelId;
        _chunkSizeBytes = chunkSizeBytes;
        _workingDirectory = workingDirectory;

        _workQueue = Channel.CreateBounded<Func<CancellationToken, Task>>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        for (var i = 0; i < workerCount; i++)
        {
            _workers.Add(Task.Run(() => WorkerLoopAsync(_shutdownTokenSource.Token)));
        }
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

        await completionSource.Task;
    }

    public Task PauseAsync(string transferId)
    {
        if (_transfers.TryGetValue(transferId, out var context))
        {
            context.Pause();
            context.Item.Status = TransferStatus.Paused;
        }

        return Task.CompletedTask;
    }

    public Task ResumeAsync(string transferId)
    {
        if (_transfers.TryGetValue(transferId, out var context))
        {
            context.Resume();
            context.Item.Status = TransferStatus.InProgress;
        }

        return Task.CompletedTask;
    }

    public Task CancelAsync(string transferId)
    {
        if (_transfers.TryGetValue(transferId, out var context))
        {
            context.Cancel();
            context.Item.Status = TransferStatus.Cancelled;
        }

        return Task.CompletedTask;
    }

    private async Task WorkerLoopAsync(CancellationToken shutdownToken)
    {
        await foreach (var workItem in _workQueue.Reader.ReadAllAsync(shutdownToken))
        {
            try
            {
                await workItem(shutdownToken);
            }
            catch (OperationCanceledException)
            {
            }
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
        }
        catch (OperationCanceledException)
        {
            item.Status = TransferStatus.Cancelled;
            ReportProgress(context);
        }
        catch (Exception ex)
        {
            item.Status = TransferStatus.Failed;
            item.ErrorMessage = ex.Message;
            ReportProgress(context);
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
        }
        catch (OperationCanceledException)
        {
            item.Status = TransferStatus.Cancelled;
            ReportProgress(context);
            context.CompletionSource?.TrySetCanceled();
        }
        catch (Exception ex)
        {
            item.Status = TransferStatus.Failed;
            item.ErrorMessage = ex.Message;
            ReportProgress(context);
            context.CompletionSource?.TrySetException(ex);
        }
    }

    private static async Task<long> ExecuteWithRetryAsync(Func<Task<long>> operation, CancellationToken cancellationToken)
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
                if (attempt >= MaxRetryAttempts)
                {
                    throw;
                }

                var delay = TimeSpan.FromMilliseconds(BaseBackoffMilliseconds * Math.Pow(2, attempt - 1));
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
