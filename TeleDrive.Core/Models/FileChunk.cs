namespace TeleDrive.Core.Models;

public class FileChunk
{
    public int Index { get; set; }
    public long MessageId { get; set; }
    public long Size { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}
