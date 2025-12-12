using ChatApp.Application.DTOs.Chat;
using ChatApp.Application.DTOs.Group;
using ChatApp.Domain.Enums;

namespace ChatApp.Application.Interfaces.IRepositories;

public interface IChatRepository
{
    Task<Guid> GetOrCreateDirectConversationAsync(Guid userA, Guid userB);
    Task<long> SaveMessageAsync(Guid conversationId, Guid senderId, string body, string contentType, string? mediaUrl = null);
    Task<IEnumerable<ContactDto>> GetContactsAsync(Guid userId);
    Task<IEnumerable<DTOs.Chat.MessageWithStatusDto>> GetMessagesWithStatusAsync(Guid conversationId, Guid userId, int page, int pageSize);
    Task<IEnumerable<Guid>> MarkMessagesAsReadAsync(Guid conversationId, Guid userId, long lastReadMessageId);
    Task<(IEnumerable<SearchResultDto> Results, int TotalCount)> SearchMessagesAsync(
        Guid userId,
        string query,
        Guid? senderId,
        Guid? conversationId,
        DateTime? startDate,
        DateTime? endDate,
        int page,
        int pageSize);

    // Group operations
    Task<(Guid ConversationId, string? ErrorMessage)> CreateGroupAsync(Guid creatorUserId, string groupName, string? groupPhotoUrl, List<Guid> memberUserIds);
    Task<GroupDetailsDto?> GetGroupDetailsAsync(Guid conversationId, Guid userId);
    Task<string?> AddGroupMemberAsync(Guid conversationId, Guid userId, Guid addedBy);
    Task<string?> RemoveGroupMemberAsync(Guid conversationId, Guid userId, Guid removedBy);
    Task<string?> LeaveGroupAsync(Guid conversationId, Guid userId);
    Task<bool> IsUserAdminAsync(Guid conversationId, Guid userId);
    Task<string?> TransferAdminAsync(Guid conversationId, Guid oldAdminId, Guid newAdminId);
    Task<string?> UpdateGroupInfoAsync(Guid conversationId, Guid userId, string? groupName, string? groupPhotoUrl);

    // Message operations
    Task UpdateMessageStatusAsync(long messageId, Guid userId, string status);
    Task<Guid?> GetSenderIdByMessageIdAsync(long messageId);
    Task<IEnumerable<DTOs.Chat.MessageStatusDto>> GetMessageStatusAsync(long messageId);
    Task<string?> DeleteMessageAsync(long messageId, Guid userId, bool deleteForEveryone);
    Task<string?> EditMessageAsync(long messageId, Guid userId, string newBody);
    Task<(long? MessageId, string? ErrorMessage)> ForwardMessageAsync(long originalMessageId, Guid forwardedBy, Guid targetConversationId);
    Task<Guid> GetConversationIdByMessageIdAsync(long messageId);
    Task<List<Guid>> GetConversationMembersAsync(Guid conversationId);
}