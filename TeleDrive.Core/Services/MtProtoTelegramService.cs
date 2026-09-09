using TeleDrive.Core.Interfaces;
using TL;
using WTelegram;

namespace TeleDrive.Core.Services;

/// <summary>
/// ITelegramService implementation backed by the MTProto user account API via WTelegramClient.
/// Login (phone, OTP, optional 2FA password) is driven by the configFunc callback supplied by the wizard.
/// </summary>
public class MtProtoTelegramService : ITelegramService, IDisposable
{
    private readonly Client _client;
    private User? _user;

    public MtProtoTelegramService(int apiId, string apiHash, Func<string, string?> configFunc)
    {
        _client = new Client(what =>
        {
            return what switch
            {
                "api_id" => apiId.ToString(),
                "api_hash" => apiHash,
                _ => configFunc(what)
            };
        });
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            _user = await _client.LoginUserIfNeeded();
            return _user is not null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<long> UploadFileAsync(string filePath, long channelId, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        await EnsureLoggedInAsync();

        var peer = await ResolveChannelAsync(channelId);

        await using var stream = File.OpenRead(filePath);
        var fileName = Path.GetFileName(filePath);

        var inputFile = await _client.UploadFileAsync(stream, fileName, progress is null
            ? null
            : (transmitted, total) => progress.Report(transmitted));

        var media = new InputMediaUploadedDocument
        {
            file = inputFile,
            mime_type = "application/octet-stream",
            attributes = new DocumentAttribute[] { new DocumentAttributeFilename { file_name = fileName } }
        };

        var update = await _client.Messages_SendMedia(peer, media, string.Empty, WTelegram.Helpers.RandomLong());
        var messageId = ExtractMessageId(update);

        return messageId;
    }

    public async Task DownloadFileAsync(long messageId, long channelId, string destinationPath, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        await EnsureLoggedInAsync();

        var peer = await ResolveChannelAsync(channelId);
        var messages = await _client.Channels_GetMessages(peer, new InputMessage[] { (int)messageId });

        if (messages.Messages.FirstOrDefault() is not TL.Message { media: MessageMediaDocument { document: TL.Document document } })
        {
            throw new InvalidOperationException("Message does not contain a document.");
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var destinationStream = File.Create(destinationPath);
        await _client.DownloadFileAsync(document, destinationStream, progress: progress is null
            ? null
            : (transmitted, total) => progress.Report(transmitted));
    }

    public async Task DeleteMessageAsync(long messageId, long channelId, CancellationToken cancellationToken)
    {
        await EnsureLoggedInAsync();

        var peer = await ResolveChannelAsync(channelId);
        await _client.Channels_DeleteMessages(peer, new[] { (int)messageId });
    }

    public async Task<long> SendTextAsync(string text, long channelId, CancellationToken cancellationToken)
    {
        await EnsureLoggedInAsync();

        var peer = await ResolveChannelAsync(channelId);
        var update = await _client.Messages_SendMessage(peer, text, WTelegram.Helpers.RandomLong());
        return ExtractMessageId(update);
    }

    public async Task EditMessageTextAsync(long messageId, long channelId, string newText, CancellationToken cancellationToken)
    {
        await EnsureLoggedInAsync();

        var peer = await ResolveChannelAsync(channelId);
        await _client.Messages_EditMessage(peer, (int)messageId, newText);
    }

    public async Task PinMessageAsync(long messageId, long channelId, CancellationToken cancellationToken)
    {
        await EnsureLoggedInAsync();

        var peer = await ResolveChannelAsync(channelId);
        await _client.Messages_UpdatePinnedMessage(peer, (int)messageId);
    }

    public async Task<(long MessageId, string Text)?> GetPinnedMessageAsync(long channelId, CancellationToken cancellationToken)
    {
        await EnsureLoggedInAsync();

        var peer = await ResolveChannelAsync(channelId);
        var fullChat = await _client.Channels_GetFullChannel(peer);

        if (fullChat.full_chat is not ChannelFull channelFull || channelFull.pinned_msg_id == 0)
        {
            return null;
        }

        var messages = await _client.Channels_GetMessages(peer, new InputMessage[] { channelFull.pinned_msg_id });

        if (messages.Messages.FirstOrDefault() is TL.Message message)
        {
            return (message.id, message.message);
        }

        return null;
    }

    public async Task<long> CreateChannelAsync(string title, string about, CancellationToken cancellationToken)
    {
        await EnsureLoggedInAsync();

        var update = await _client.Channels_CreateChannel(title, about, broadcast: true);

        var users = new Dictionary<long, User>();
        var chats = new Dictionary<long, ChatBase>();
        update.CollectUsersChats(users, chats);

        var newChannel = chats.Values.OfType<Channel>().FirstOrDefault()
            ?? throw new InvalidOperationException("Channel creation did not return a channel.");

        _channelAccessHashes[newChannel.id] = newChannel.access_hash;

        return newChannel.id;
    }

    private async Task EnsureLoggedInAsync()
    {
        _user ??= await _client.LoginUserIfNeeded();
    }

    private readonly Dictionary<long, long> _channelAccessHashes = new();

    private async Task<InputChannel> ResolveChannelAsync(long channelId)
    {
        if (_channelAccessHashes.TryGetValue(channelId, out var accessHash))
        {
            return new InputChannel(channelId, accessHash);
        }

        var dialogs = await _client.Messages_GetAllDialogs();
        foreach (var chat in dialogs.chats.Values)
        {
            if (chat is Channel channel)
            {
                _channelAccessHashes[channel.id] = channel.access_hash;
            }
        }

        if (_channelAccessHashes.TryGetValue(channelId, out accessHash))
        {
            return new InputChannel(channelId, accessHash);
        }

        throw new InvalidOperationException(
            $"Unable to resolve access hash for channel {channelId}. Ensure this account is a member of the vault channel.");
    }

    private static long ExtractMessageId(UpdatesBase update)
    {
        var messageUpdate = update.UpdateList
            .OfType<UpdateNewMessage>()
            .FirstOrDefault();

        return messageUpdate?.message.ID ?? 0;
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
