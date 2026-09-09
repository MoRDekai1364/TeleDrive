using TeleDrive.Core.Models;

namespace TeleDrive.Core.Interfaces;

public interface IIndexService
{
    /// <summary>
    /// Reads the current file index from the pinned index message in the vault channel.
    /// </summary>
    /// <param name="channelId">Vault channel identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>List of all files currently tracked in the index.</returns>
    Task<List<VaultFile>> ReadIndexAsync(long channelId, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the given file list back to the pinned index message in the vault channel.
    /// </summary>
    /// <param name="files">Complete, up-to-date list of files to persist.</param>
    /// <param name="channelId">Vault channel identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task WriteIndexAsync(List<VaultFile> files, long channelId, CancellationToken cancellationToken);

    /// <summary>
    /// Rebuilds the local SQLite cache from the remote index.
    /// </summary>
    /// <param name="channelId">Vault channel identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task RebuildLocalCacheAsync(long channelId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the file list from the local SQLite cache without contacting Telegram.
    /// </summary>
    /// <returns>List of files as of the last cache rebuild or update.</returns>
    Task<List<VaultFile>> ReadLocalCacheAsync();
}
