using ChatApp.Domain.Chat;
using ChatApp.Infrastructure.Repositories;

namespace ChatApp.Application.Services;

public class ChatService : IChatService
{
    private readonly IChatRepository _chat;
    public ChatService(IChatRepository chat) => _chat = chat;

    public async Task<(Guid ConversationId, long MessageId)> SendDirectAsync(Guid fromUserId, Guid toUserId, string body)
    {
        var convId = await _chat.GetOrCreateDirectConversationAsync(fromUserId, toUserId);
        var msgId = await _chat.SaveMessageAsync(convId, fromUserId, body, "text");
        return (convId, msgId);
    }

    public async Task<(Guid ConversationId, long MessageId)> SendGroupMessageAsync(Guid conversationId, Guid senderId, string body)
    {
        var msgId = await _chat.SaveMessageAsync(conversationId, senderId, body, "text");
        return (conversationId, msgId);
    }

    public async Task<IEnumerable<ContactDto>> GetContactsAsync(Guid userId)
        => await _chat.GetContactsAsync(userId);

    public async Task<IEnumerable<MessageWithStatusDto>> GetChatHistoryAsync(Guid conversationId, Guid userId, int page, int pageSize)
        => await _chat.GetMessagesWithStatusAsync(conversationId, userId, page, pageSize);

    public async Task<Guid> GetOrCreateConversationAsync(Guid userA, Guid userB)
        => await _chat.GetOrCreateDirectConversationAsync(userA, userB);

    //public async Task MarkMessagesAsReadAsync(Guid conversationId, Guid userId, long lastReadMessageId)
    //{
    //    await _chat.MarkMessagesAsReadAsync(conversationId, userId, lastReadMessageId);
    //}

    public async Task<IEnumerable<Guid>> MarkMessagesAsReadAsync(Guid conversationId, Guid userId, long lastReadMessageId)
    {
        return await _chat.MarkMessagesAsReadAsync(conversationId, userId, lastReadMessageId);
    }

    // Group methods
    public async Task<(Guid? ConversationId, string? ErrorMessage)> CreateGroupAsync(Guid creatorUserId, string groupName, string? groupPhotoUrl, List<Guid> memberUserIds)
    {
        var (convId, error) = await _chat.CreateGroupAsync(creatorUserId, groupName, groupPhotoUrl, memberUserIds);
        return error == null ? (convId, null) : (null, error);
    }

    public async Task<GroupDetailsDto?> GetGroupDetailsAsync(Guid conversationId, Guid userId)
        => await _chat.GetGroupDetailsAsync(conversationId, userId);

    public async Task<string?> AddGroupMemberAsync(Guid conversationId, Guid userId, Guid addedBy)
        => await _chat.AddGroupMemberAsync(conversationId, userId, addedBy);

    public async Task<string?> RemoveGroupMemberAsync(Guid conversationId, Guid userId, Guid removedBy)
        => await _chat.RemoveGroupMemberAsync(conversationId, userId, removedBy);

    public async Task<string?> UpdateGroupInfoAsync(Guid conversationId, Guid userId, string? groupName, string? groupPhotoUrl)
        => await _chat.UpdateGroupInfoAsync(conversationId, userId, groupName, groupPhotoUrl);

    // Message status
    public async Task UpdateMessageStatusAsync(long messageId, Guid userId, string status)
        => await _chat.UpdateMessageStatusAsync(messageId, userId, status);
    public async Task<Guid?> GetSenderIdByMessageIdAsync(long messageId)
    => await _chat.GetSenderIdByMessageIdAsync(messageId);


    public async Task<IEnumerable<MessageStatusDto>> GetMessageStatusAsync(long messageId)
        => await _chat.GetMessageStatusAsync(messageId);
}