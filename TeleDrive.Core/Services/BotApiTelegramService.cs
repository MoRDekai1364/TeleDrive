using Telegram.Bot;
using Telegram.Bot.Types;
using TeleDrive.Core.Interfaces;

namespace TeleDrive.Core.Services;

/// <summary>
/// ITelegramService implementation backed by the Telegram Bot API.
/// </summary>
public class BotApiTelegramService : ITelegramService
{
    private readonly TelegramBotClient _client;

    public BotApiTelegramService(string botToken)
    {
        _client = new TelegramBotClient(botToken);
    }

    public async Task<long> UploadFileAsync(string filePath, long channelId, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var fileName = Path.GetFileName(filePath);

        var message = await _client.SendDocument(
            chatId: channelId,
            document: InputFile.FromStream(stream, fileName),
            cancellationToken: cancellationToken);

        return message.MessageId;
    }

    public async Task DownloadFileAsync(long messageId, long channelId, string destinationPath, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        var message = await _client.ForwardMessage(
            chatId: channelId,
            fromChatId: channelId,
            messageId: (int)messageId,
            cancellationToken: cancellationToken);

        var fileId = message.Document?.FileId
            ?? throw new InvalidOperationException("Message does not contain a document.");

        var file = await _client.GetFile(fileId, cancellationToken);

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var destinationStream = File.Create(destinationPath);
        await _client.DownloadFile(file, destinationStream, cancellationToken);

        await _client.DeleteMessage(channelId, message.MessageId, cancellationToken);
    }

    public async Task DeleteMessageAsync(long messageId, long channelId, CancellationToken cancellationToken)
    {
        await _client.DeleteMessage(channelId, (int)messageId, cancellationToken);
    }

    public async Task<long> SendTextAsync(string text, long channelId, CancellationToken cancellationToken)
    {
        var message = await _client.SendMessage(channelId, text, cancellationToken: cancellationToken);
        return message.MessageId;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _client.GetMe(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task EditMessageTextAsync(long messageId, long channelId, string newText, CancellationToken cancellationToken)
    {
        await _client.EditMessageText(channelId, (int)messageId, newText, cancellationToken: cancellationToken);
    }

    public async Task PinMessageAsync(long messageId, long channelId, CancellationToken cancellationToken)
    {
        await _client.PinChatMessage(channelId, (int)messageId, cancellationToken: cancellationToken);
    }

    public async Task<(long MessageId, string Text)?> GetPinnedMessageAsync(long channelId, CancellationToken cancellationToken)
    {
        var chat = await _client.GetChat(channelId, cancellationToken);

        if (chat.PinnedMessage is null)
        {
            return null;
        }

        return (chat.PinnedMessage.MessageId, chat.PinnedMessage.Text ?? string.Empty);
    }
}
