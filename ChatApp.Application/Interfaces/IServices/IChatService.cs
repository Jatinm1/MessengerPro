using ChatApp.Application.DTOs.Chat;
using ChatApp.Application.DTOs.Group;

namespace ChatApp.Application.Interfaces.IServices;

public interface IChatService
{
    // Direct messaging
    Task<IEnumerable<ContactDto>> GetContactsAsync(Guid userId);
    Task<IEnumerable<MessageWithStatusDto>> GetChatHistoryAsync(Guid conversationId, Guid userId, int page, int pageSize);
    Task<Guid> GetOrCreateConversationAsync(Guid userA, Guid userB);

    // Message operations - SIMPLIFIED FOR HUB
    Task<long> SaveMessageAsync(Guid conversationId, Guid senderId, string body, string contentType, string? mediaUrl = null);
    Task<IEnumerable<Guid>> MarkMessagesAsReadAsync(Guid conversationId, Guid userId, long lastReadMessageId);
    Task UpdateMessageStatusAsync(long messageId, Guid userId, string status);
    Task<Guid?> GetSenderIdByMessageIdAsync(long messageId);
    Task<IEnumerable<MessageStatusDto>> GetMessageStatusAsync(long messageId);
    Task<Guid> GetConversationIdByMessageIdAsync(long messageId);
    Task<List<Guid>> GetConversationMembersAsync(Guid conversationId);

    // Message actions
    Task<string?> DeleteMessageAsync(long messageId, Guid userId, bool deleteForEveryone);
    Task<string?> EditMessageAsync(long messageId, Guid userId, string newBody);
    Task<(long? MessageId, string? ErrorMessage)> ForwardMessageAsync(long originalMessageId, Guid forwardedBy, Guid targetConversationId);

    Task<SearchResponseDto> SearchMessagesAsync(
       Guid userId,
       string query,
       Guid? senderId,
       Guid? conversationId,
       DateTime? startDate,
       DateTime? endDate,
       int page,
       int pageSize);

    // Group management
    Task<(Guid? ConversationId, string? ErrorMessage)> CreateGroupAsync(Guid creatorUserId, string groupName, string? groupPhotoUrl, List<Guid> memberUserIds);
    Task<GroupDetailsDto?> GetGroupDetailsAsync(Guid conversationId, Guid userId);
    Task<string?> AddGroupMemberAsync(Guid conversationId, Guid userId, Guid addedBy);
    Task<string?> RemoveGroupMemberAsync(Guid conversationId, Guid userId, Guid removedBy);
    Task<string?> LeaveGroupAsync(Guid conversationId, Guid userId);
    Task<bool> IsUserAdminAsync(Guid conversationId, Guid userId);
    Task<string?> TransferAdminAsync(Guid conversationId, Guid oldAdminId, Guid newAdminId);
    Task<string?> UpdateGroupInfoAsync(Guid conversationId, Guid userId, string? groupName, string? groupPhotoUrl);
    Task<string?> DeleteGroupAsync(Guid conversationId, Guid userId);
}