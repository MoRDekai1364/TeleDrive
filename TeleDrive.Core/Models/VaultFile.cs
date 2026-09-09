namespace TeleDrive.Core.Models;

public class VaultFile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Mime { get; set; } = string.Empty;
    public DateTimeOffset Uploaded { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public List<FileChunk> Chunks { get; set; } = new();
}
