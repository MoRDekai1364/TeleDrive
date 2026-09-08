namespace TeleDrive.Core.Models;

public class PendingTransferRecord
{
    public string Id { get; set; } = string.Empty;
    public TransferDirection Direction { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? LocalPath { get; set; }
    public string? VaultFileId { get; set; }
    public long TotalBytes { get; set; }
}
