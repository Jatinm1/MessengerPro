using ChatApp.Domain.Chat;

namespace ChatApp.Infrastructure.Repositories
{
    public interface IChatRepository
    {
        // Conversation
        Task<Guid> GetOrCreateDirectConversationAsync(Guid a, Guid b);

        // Message Saving
        Task<long> SaveMessageAsync(Guid conversationId, Guid senderId, string body, string contentType);
        Task<long> SaveMessageAsync(Guid conversationId, Guid senderId, string body, string contentType, string? mediaUrl);

        // Contacts & Chat History
        Task<IEnumerable<ContactDto>> GetContactsAsync(Guid userId);
        Task<IEnumerable<MessageWithStatusDto>> GetMessagesWithStatusAsync(Guid conversationId, Guid userId, int page, int pageSize);

        // Read Receipts
        Task<IEnumerable<Guid>> MarkMessagesAsReadAsync(Guid conversationId, Guid userId, long lastReadMessageId);

        // Group Management
        Task<(Guid ConversationId, string? ErrorMessage)> CreateGroupAsync(Guid creatorUserId, string groupName, string? groupPhotoUrl, List<Guid> memberUserIds);
        Task<GroupDetailsDto?> GetGroupDetailsAsync(Guid conversationId, Guid userId);
        Task<string?> AddGroupMemberAsync(Guid conversationId, Guid userId, Guid addedBy);
        Task<string?> RemoveGroupMemberAsync(Guid conversationId, Guid userId, Guid removedBy);
        Task<string?> LeaveGroupAsync(Guid conversationId, Guid userId);
        Task<string?> UpdateGroupInfoAsync(Guid conversationId, Guid userId, string? groupName, string? groupPhotoUrl);
        Task<bool> IsUserAdminAsync(Guid conversationId, Guid userId);
        Task<string?> TransferAdminAsync(Guid conversationId, Guid oldAdminId, Guid newAdminId);


        // Message Status
        Task UpdateMessageStatusAsync(long messageId, Guid userId, string status);
        Task<IEnumerable<MessageStatusDto>> GetMessageStatusAsync(long messageId);
        Task<Guid?> GetSenderIdByMessageIdAsync(long messageId);

        // Add to ChatApp.Infrastructure/Repositories/IChatRepository.cs

        Task<string?> DeleteMessageAsync(long messageId, Guid userId, bool deleteForEveryone);
        Task<string?> EditMessageAsync(long messageId, Guid userId, string newBody);
        Task<(long? MessageId, string? ErrorMessage)> ForwardMessageAsync(long originalMessageId, Guid forwardedBy, Guid targetConversationId);
        Task<Guid> GetConversationIdByMessageIdAsync(long messageId);
        Task<List<Guid>> GetConversationMembersAsync(Guid conversationId);
    }
}
