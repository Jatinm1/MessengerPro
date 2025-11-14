// ChatApp.Infrastructure/Repositories/IChatRepository.cs
using ChatApp.Domain.Chat;
namespace ChatApp.Infrastructure.Repositories
{
    public interface IChatRepository
    {
        Task<Guid> GetOrCreateDirectConversationAsync(Guid a, Guid b);
        Task<long> SaveMessageAsync(Guid conversationId, Guid senderId, string body, string contentType);
        Task<IEnumerable<ContactDto>> GetContactsAsync(Guid userId);
        Task<IEnumerable<MessageWithStatusDto>> GetMessagesWithStatusAsync(Guid conversationId, Guid userId, int page, int pageSize);
        //Task MarkMessagesAsReadAsync(Guid conversationId, Guid userId, long lastReadMessageId);
        Task<IEnumerable<Guid>> MarkMessagesAsReadAsync(Guid conversationId, Guid userId, long lastReadMessageId);


        // Group methods
        Task<(Guid ConversationId, string? ErrorMessage)> CreateGroupAsync(Guid creatorUserId, string groupName, string? groupPhotoUrl, List<Guid> memberUserIds);
        Task<GroupDetailsDto?> GetGroupDetailsAsync(Guid conversationId, Guid userId);
        Task<string?> AddGroupMemberAsync(Guid conversationId, Guid userId, Guid addedBy);
        Task<string?> RemoveGroupMemberAsync(Guid conversationId, Guid userId, Guid removedBy);
        Task<string?> UpdateGroupInfoAsync(Guid conversationId, Guid userId, string? groupName, string? groupPhotoUrl);

        // Message status methods
        Task UpdateMessageStatusAsync(long messageId, Guid userId, string status);
        Task<IEnumerable<MessageStatusDto>> GetMessageStatusAsync(long messageId);
        Task<Guid?> GetSenderIdByMessageIdAsync(long messageId);

    }
}