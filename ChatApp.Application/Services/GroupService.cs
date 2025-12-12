using ChatApp.Application.DTOs.Group;
using ChatApp.Application.Interfaces.IRepositories;
using ChatApp.Domain.Entities;

namespace ChatApp.Application.Services;

/// <summary>
/// Service responsible for group management operations including group creation,
/// member management, and group information updates.
/// </summary>
public class GroupService : IGroupService
{
    private readonly IGroupRepository _groupRepository;

    /// <summary>
    /// Initializes a new instance of GroupService with group repository.
    /// </summary>
    public GroupService(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    /// <summary>
    /// Creates a new group chat with specified members.
    /// Returns conversation ID on success, error message on failure.
    /// </summary>
    public async Task<(Guid? ConversationId, string? ErrorMessage)> CreateGroupAsync(
        Guid creatorUserId,
        string groupName,
        string? groupPhotoUrl,
        List<Guid> memberUserIds)
    {
        var (convId, error) = await _groupRepository.CreateGroupAsync(creatorUserId, groupName, groupPhotoUrl, memberUserIds);
        return error == null ? (convId, null) : (null, error);
    }

    /// <summary>
    /// Retrieves detailed information about a specific group.
    /// Returns null if group doesn't exist or user is not a member.
    /// </summary>
    public async Task<GroupDetailsDto?> GetGroupDetailsAsync(Guid conversationId, Guid userId)
        => await _groupRepository.GetGroupDetailsAsync(conversationId, userId);

    /// <summary>
    /// Adds a new member to an existing group.
    /// Returns error message if addition fails, null on success.
    /// </summary>
    public async Task<string?> AddGroupMemberAsync(Guid conversationId, Guid userId, Guid addedBy)
        => await _groupRepository.AddGroupMemberAsync(conversationId, userId, addedBy);

    /// <summary>
    /// Removes a member from a group.
    /// Returns error message if removal fails, null on success.
    /// </summary>
    public async Task<string?> RemoveGroupMemberAsync(Guid conversationId, Guid userId, Guid removedBy)
        => await _groupRepository.RemoveGroupMemberAsync(conversationId, userId, removedBy);

    /// <summary>
    /// Updates group information (name and/or photo URL).
    /// Returns error message if update fails, null on success.
    /// </summary>
    public async Task<string?> UpdateGroupInfoAsync(Guid conversationId, Guid userId, string? groupName, string? groupPhotoUrl)
        => await _groupRepository.UpdateGroupInfoAsync(conversationId, userId, groupName, groupPhotoUrl);

    /// <summary>
    /// Updates only the group's profile photo.
    /// Returns error message if update fails, null on success.
    /// </summary>
    public async Task<string?> UpdateGroupPhotoAsync(Guid conversationId, Guid userId, string groupPhotoUrl)
        => await _groupRepository.UpdateGroupPhotoAsync(conversationId, userId, groupPhotoUrl);

    /// <summary>
    /// Permanently deletes a group.
    /// Returns error message if deletion fails, null on success.
    /// </summary>
    public async Task<string?> DeleteGroupAsync(Guid conversationId, Guid userId)
        => await _groupRepository.DeleteGroupAsync(conversationId, userId);
}