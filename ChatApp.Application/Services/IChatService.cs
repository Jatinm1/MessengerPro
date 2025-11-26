// ChatApp.Application/Services/IChatService.cs
using ChatApp.Domain.Chat;
namespace ChatApp.Application.Services
{

    public interface IChatService
    {
        Task<(Guid ConversationId, long MessageId)> SendDirectAsync(Guid fromUserId, Guid toUserId, string body);
        Task<(Guid ConversationId, long MessageId)> SendGroupMessageAsync(Guid conversationId, Guid senderId, string body);
        Task<IEnumerable<ContactDto>> GetContactsAsync(Guid userId);
        Task<IEnumerable<MessageWithStatusDto>> GetChatHistoryAsync(Guid conversationId, Guid userId, int page, int pageSize);
        Task<bool> IsUserAdminAsync(Guid conversationId, Guid userId);
        Task<string?> TransferAdminAsync(Guid conversationId, Guid oldAdminId, Guid newAdminId);

        // Existing methods...
        //Task<(Guid ConversationId, long MessageId)> SendDirectAsync(Guid fromUserId, Guid toUserId, string body);

        // New overload with media support
        Task<(Guid ConversationId, long MessageId)> SendDirectAsync(Guid fromUserId, Guid toUserId, string body, string contentType, string? mediaUrl);

        //Task<(Guid ConversationId, long MessageId)> SendGroupMessageAsync(Guid conversationId, Guid senderId, string body);

        // New overload with media support
        Task<(Guid ConversationId, long MessageId)> SendGroupMessageAsync(Guid conversationId, Guid senderId, string body, string contentType, string? mediaUrl);
        Task<Guid> GetOrCreateConversationAsync(Guid userA, Guid userB);
        //Task MarkMessagesAsReadAsync(Guid conversationId, Guid userId, long lastReadMessageId);
        Task<IEnumerable<Guid>> MarkMessagesAsReadAsync(Guid conversationId, Guid userId, long lastReadMessageId);


        // Group methods
        Task<(Guid? ConversationId, string? ErrorMessage)> CreateGroupAsync(Guid creatorUserId, string groupName, string? groupPhotoUrl, List<Guid> memberUserIds);
        Task<GroupDetailsDto?> GetGroupDetailsAsync(Guid conversationId, Guid userId);
        Task<string?> AddGroupMemberAsync(Guid conversationId, Guid userId, Guid addedBy);
        Task<string?> RemoveGroupMemberAsync(Guid conversationId, Guid userId, Guid removedBy);
        Task<string?> LeaveGroupAsync(Guid conversationId, Guid userId);
        Task<string?> UpdateGroupInfoAsync(Guid conversationId, Guid userId, string? groupName, string? groupPhotoUrl);

        // Message status
        Task UpdateMessageStatusAsync(long messageId, Guid userId, string status);
        Task<IEnumerable<MessageStatusDto>> GetMessageStatusAsync(long messageId);
        Task<Guid?> GetSenderIdByMessageIdAsync(long messageId);

        Task<string?> DeleteGroupAsync(Guid conversationId, Guid userId);


    }
}
