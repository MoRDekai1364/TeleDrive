using TeleDrive.Core.Helpers;
using TeleDrive.Core.Interfaces;
using TeleDrive.Core.Models;

namespace TeleDrive.Core.Services;

/// <summary>
/// Splits and reassembles files via streamed I/O, with SHA-256 hashing per chunk and whole file.
/// </summary>
public class ChunkingService : IChunkingService
{
    private const int BufferSize = 81920;

    public async Task<List<FileChunk>> SplitAsync(string filePath, long chunkSizeBytes, string workingDirectory, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Source file not found.", filePath);
        }

        if (chunkSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSizeBytes));
        }

        if (!Directory.Exists(workingDirectory))
        {
            Directory.CreateDirectory(workingDirectory);
        }

        var chunks = new List<FileChunk>();
        var fileInfo = new FileInfo(filePath);
        var chunkIndex = 0;

        await using var sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);

        var remaining = fileInfo.Length;
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentChunkSize = Math.Min(chunkSizeBytes, remaining);
            var chunkPath = GetChunkPath(workingDirectory, chunkIndex);

            await using (var chunkStream = new FileStream(chunkPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
            {
                var bytesLeftForChunk = currentChunkSize;
                var buffer = new byte[BufferSize];

                while (bytesLeftForChunk > 0)
                {
                    var bytesToRead = (int)Math.Min(buffer.Length, bytesLeftForChunk);
                    var bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, bytesToRead), cancellationToken);

                    if (bytesRead == 0)
                    {
                        throw new IOException("Unexpected end of stream while reading chunk.");
                    }

                    await chunkStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    bytesLeftForChunk -= bytesRead;
                }
            }

            var chunkHash = await HashHelper.ComputeFileSha256Async(chunkPath, cancellationToken);

            chunks.Add(new FileChunk
            {
                Index = chunkIndex,
                MessageId = 0,
                Size = currentChunkSize,
                Sha256 = chunkHash
            });

            remaining -= currentChunkSize;
            chunkIndex++;
        }

        return chunks;
    }

    public async Task ReassembleAsync(List<FileChunk> chunks, List<string> chunkFilePaths, string destinationPath, CancellationToken cancellationToken)
    {
        if (chunks.Count != chunkFilePaths.Count)
        {
            throw new ArgumentException("Chunk metadata count must match chunk file path count.");
        }

        var orderedPairs = chunks
            .Select((chunk, position) => (chunk, path: chunkFilePaths[position]))
            .OrderBy(pair => pair.chunk.Index)
            .ToList();

        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        await using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);

        foreach (var (chunk, path) in orderedPairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Chunk file for index {chunk.Index} not found.", path);
            }

            var actualHash = await HashHelper.ComputeFileSha256Async(path, cancellationToken);
            if (!HashHelper.VerifyHash(chunk.Sha256, actualHash))
            {
                throw new InvalidDataException($"Chunk {chunk.Index} failed hash verification.");
            }

            await using var chunkStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
            await chunkStream.CopyToAsync(destinationStream, BufferSize, cancellationToken);
        }
    }

    public Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken)
    {
        return HashHelper.ComputeFileSha256Async(filePath, cancellationToken);
    }

    private static string GetChunkPath(string workingDirectory, int index)
    {
        return Path.Combine(workingDirectory, $"{index}.chunk");
    }
}
