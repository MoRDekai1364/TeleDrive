using TeleDrive.Core.Models;

namespace TeleDrive.Core.Interfaces;

public interface ITelegramService
{
    /// <summary>
    /// Uploads a file as a document message to the vault channel.
    /// </summary>
    /// <param name="filePath">Absolute path of the local file to upload.</param>
    /// <param name="channelId">Target channel identifier.</param>
    /// <param name="progress">Optional progress reporter for bytes sent.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The message id of the uploaded document.</returns>
    Task<long> UploadFileAsync(string filePath, long channelId, IProgress<long>? progress, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads a document message to a local file path.
    /// </summary>
    /// <param name="messageId">Identifier of the message containing the document.</param>
    /// <param name="channelId">Channel identifier the message belongs to.</param>
    /// <param name="destinationPath">Absolute path to write the downloaded file.</param>
    /// <param name="progress">Optional progress reporter for bytes received.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task DownloadFileAsync(long messageId, long channelId, string destinationPath, IProgress<long>? progress, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a message from a channel.
    /// </summary>
    /// <param name="messageId">Identifier of the message to delete.</param>
    /// <param name="channelId">Channel identifier the message belongs to.</param>
    Task DeleteMessageAsync(long messageId, long channelId, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a plain text message to a channel.
    /// </summary>
    /// <param name="text">Text content to send.</param>
    /// <param name="channelId">Target channel identifier.</param>
    /// <returns>The message id of the sent text message.</returns>
    Task<long> SendTextAsync(string text, long channelId, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies that the current credentials can authenticate and reach Telegram.
    /// </summary>
    /// <returns>True if the connection is valid.</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Edits the text content of an existing text message.
    /// </summary>
    /// <param name="messageId">Identifier of the message to edit.</param>
    /// <param name="channelId">Channel identifier the message belongs to.</param>
    /// <param name="newText">Replacement text content.</param>
    Task EditMessageTextAsync(long messageId, long channelId, string newText, CancellationToken cancellationToken);

    /// <summary>
    /// Pins a message in a channel.
    /// </summary>
    /// <param name="messageId">Identifier of the message to pin.</param>
    /// <param name="channelId">Channel identifier the message belongs to.</param>
    Task PinMessageAsync(long messageId, long channelId, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the currently pinned message in a channel, if any.
    /// </summary>
    /// <param name="channelId">Channel identifier to query.</param>
    /// <returns>The message id and text of the pinned message, or null if none is pinned.</returns>
    Task<(long MessageId, string Text)?> GetPinnedMessageAsync(long channelId, CancellationToken cancellationToken);
}
