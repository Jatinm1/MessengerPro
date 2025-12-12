using ChatApp.Application.DTOs.Group;

namespace ChatApp.Application.Interfaces.IRepositories;

public interface IGroupRepository
{
    Task<(Guid ConversationId, string? ErrorMessage)> CreateGroupAsync(Guid creatorUserId, string groupName, string? groupPhotoUrl, List<Guid> memberUserIds);
    Task<GroupDetailsDto?> GetGroupDetailsAsync(Guid conversationId, Guid userId);
    Task<string?> AddGroupMemberAsync(Guid conversationId, Guid userId, Guid addedBy);
    Task<string?> RemoveGroupMemberAsync(Guid conversationId, Guid userId, Guid removedBy);
    Task<string?> UpdateGroupInfoAsync(Guid conversationId, Guid userId, string? groupName, string? groupPhotoUrl);
    Task<string?> UpdateGroupPhotoAsync(Guid conversationId, Guid userId, string groupPhotoUrl);
    Task<string?> DeleteGroupAsync(Guid conversationId, Guid userId);
}