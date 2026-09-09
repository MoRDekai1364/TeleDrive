using System.Security.Cryptography;

namespace TeleDrive.Core.Helpers;

public static class HashHelper
{
    public static async Task<string> ComputeFileSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static string ComputeBytesSha256(byte[] data)
    {
        var hashBytes = SHA256.HashData(data);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static bool VerifyHash(string expectedHash, string actualHash)
    {
        return string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase);
    }
}
