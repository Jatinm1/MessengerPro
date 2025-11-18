using ChatApp.Domain.Chat;
using ChatApp.Infrastructure.Repositories;

namespace ChatApp.Application.Services;

public class GroupService : IGroupService
{
    private readonly IGroupRepository _groupRepository;

    public GroupService(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<(Guid? ConversationId, string? ErrorMessage)> CreateGroupAsync(
        Guid creatorUserId,
        string groupName,
        string? groupPhotoUrl,
        List<Guid> memberUserIds)
    {
        var (convId, error) = await _groupRepository.CreateGroupAsync(creatorUserId, groupName, groupPhotoUrl, memberUserIds);
        return error == null ? (convId, null) : (null, error);
    }

    public async Task<GroupDetailsDto?> GetGroupDetailsAsync(Guid conversationId, Guid userId)
        => await _groupRepository.GetGroupDetailsAsync(conversationId, userId);

    public async Task<string?> AddGroupMemberAsync(Guid conversationId, Guid userId, Guid addedBy)
        => await _groupRepository.AddGroupMemberAsync(conversationId, userId, addedBy);

    public async Task<string?> RemoveGroupMemberAsync(Guid conversationId, Guid userId, Guid removedBy)
        => await _groupRepository.RemoveGroupMemberAsync(conversationId, userId, removedBy);

    public async Task<string?> UpdateGroupInfoAsync(Guid conversationId, Guid userId, string? groupName, string? groupPhotoUrl)
        => await _groupRepository.UpdateGroupInfoAsync(conversationId, userId, groupName, groupPhotoUrl);

    public async Task<string?> UpdateGroupPhotoAsync(Guid conversationId, Guid userId, string groupPhotoUrl)
        => await _groupRepository.UpdateGroupPhotoAsync(conversationId, userId, groupPhotoUrl);
}