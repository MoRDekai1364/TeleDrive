namespace TeleDrive.Core.Models;

public enum TransferDirection
{
    Upload,
    Download
}

public enum TransferStatus
{
    Queued,
    InProgress,
    Paused,
    Completed,
    Failed,
    Cancelled
}

public class TransferItem
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long TransferredBytes { get; set; }
    public TransferDirection Direction { get; set; }
    public TransferStatus Status { get; set; }
    public double SpeedBytesPerSecond { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public string? ErrorMessage { get; set; }
    public string? LocalPath { get; set; }
    public string? VaultFileId { get; set; }

    public bool IsInProgress => Status == TransferStatus.InProgress;
    public bool IsPaused => Status == TransferStatus.Paused;
    public bool IsActive => Status is TransferStatus.InProgress or TransferStatus.Paused or TransferStatus.Queued;
}
