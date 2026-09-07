using TeleDrive.Core.Models;

namespace TeleDrive.Core.Interfaces;

public interface IChunkingService
{
    /// <summary>
    /// Splits a file into fixed-size chunks written to a working directory.
    /// </summary>
    /// <param name="filePath">Absolute path of the source file.</param>
    /// <param name="chunkSizeBytes">Maximum size of each chunk in bytes.</param>
    /// <param name="workingDirectory">Directory to write chunk files into.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Ordered list of chunk file paths and their metadata.</returns>
    Task<List<FileChunk>> SplitAsync(string filePath, long chunkSizeBytes, string workingDirectory, CancellationToken cancellationToken);

    /// <summary>
    /// Reassembles chunk files into a single output file at the correct offsets.
    /// </summary>
    /// <param name="chunks">Ordered chunk metadata describing the file.</param>
    /// <param name="chunkFilePaths">Local paths of the downloaded chunk files, matching chunk order.</param>
    /// <param name="destinationPath">Absolute path to write the reassembled file.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task ReassembleAsync(List<FileChunk> chunks, List<string> chunkFilePaths, string destinationPath, CancellationToken cancellationToken);

    /// <summary>
    /// Computes the SHA-256 hash of a file.
    /// </summary>
    /// <param name="filePath">Absolute path of the file to hash.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Lowercase hex-encoded SHA-256 hash.</returns>
    Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken);
}
