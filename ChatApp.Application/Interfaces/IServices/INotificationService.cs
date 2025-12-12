using ChatApp.Application.DTOs.Chat;
using ChatApp.Application.DTOs.Group;

namespace ChatApp.Application.Interfaces.IServices;

public interface INotificationService
{
    Task NotifyMessageSentAsync(MessageSentDto message);
    Task NotifyMessageStatusUpdatedAsync(long messageId, Guid conversationId, string status);
    Task NotifyGroupCreatedAsync(GroupDetailsDto groupDetails);
    Task NotifyGroupMemberAddedAsync(Guid conversationId, Guid addedUserId, Guid addedBy);
    Task NotifyGroupMemberRemovedAsync(Guid conversationId, Guid removedUserId, Guid removedBy);
    Task NotifyGroupDeletedAsync(Guid conversationId, string groupName, Guid deletedBy, List<Guid> memberIds);
    Task NotifyMessageDeletedAsync(long messageId, Guid conversationId, Guid deletedBy, bool deleteForEveryone, List<Guid> memberIds);
    Task NotifyMessageEditedAsync(long messageId, Guid conversationId, string newBody, Guid editedBy, List<Guid> memberIds);
}
