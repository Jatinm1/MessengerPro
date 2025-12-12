using ChatApp.Application.DTOs.Chat;
using ChatApp.Application.DTOs.Group;
using ChatApp.Application.Interfaces.IRepositories;
using ChatApp.Application.Interfaces.IServices;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;

namespace ChatApp.Application.Services;

/// <summary>
/// Service handling chat operations including direct messaging, group management,
/// and message lifecycle operations (send, edit, delete, forward).
/// </summary>
public class ChatService : IChatService
{
    private readonly IChatRepository _chatRepository;
    private readonly IGroupRepository _groupRepository;

    /// <summary>
    /// Initializes a new instance of ChatService with required repositories.
    /// </summary>
    public ChatService(IChatRepository chatRepository, IGroupRepository groupRepository)
    {
        _chatRepository = chatRepository;
        _groupRepository = groupRepository;
    }

    // ========================================
    // DIRECT MESSAGING
    // ========================================

    /// <summary>
    /// Retrieves all contacts for a user (users they've interacted with).
    /// </summary>
    public async Task<IEnumerable<ContactDto>> GetContactsAsync(Guid userId)
    {
        return await _chatRepository.GetContactsAsync(userId);
    }

    /// <summary>
    /// Retrieves paginated chat history for a specific conversation.
    /// Default page size is 20 messages, maximum 100.
    /// </summary>
    public async Task<IEnumerable<MessageWithStatusDto>> GetChatHistoryAsync(
        Guid conversationId,
        Guid userId,
        int page,
        int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        return await _chatRepository.GetMessagesWithStatusAsync(conversationId, userId, page, pageSize);
    }

    /// <summary>
    /// Gets or creates a direct conversation between two users.
    /// Returns the conversation ID for existing or newly created conversation.
    /// </summary>
    public async Task<Guid> GetOrCreateConversationAsync(Guid userA, Guid userB)
    {
        return await _chatRepository.GetOrCreateDirectConversationAsync(userA, userB);
    }

    // ========================================
    // MESSAGE OPERATIONS
    // ========================================

    /// <summary>
    /// Saves a new message to the database and returns the message ID.
    /// </summary>
    public async Task<long> SaveMessageAsync(
        Guid conversationId,
        Guid senderId,
        string body,
        string contentType,
        string? mediaUrl = null)
    {
        return await _chatRepository.SaveMessageAsync(conversationId, senderId, body, contentType, mediaUrl);
    }

    /// <summary>
    /// Marks messages as read up to a specific message ID for a user in a conversation.
    /// Returns list of senders whose messages were marked as read.
    /// </summary>
    public async Task<IEnumerable<Guid>> MarkMessagesAsReadAsync(
        Guid conversationId,
        Guid userId,
        long lastReadMessageId)
    {
        return await _chatRepository.MarkMessagesAsReadAsync(conversationId, userId, lastReadMessageId);
    }

    /// <summary>
    /// Updates the delivery status of a message for a specific recipient.
    /// </summary>
    public async Task UpdateMessageStatusAsync(long messageId, Guid userId, string status)
    {
        await _chatRepository.UpdateMessageStatusAsync(messageId, userId, status);
    }

    /// <summary>
    /// Retrieves the sender ID for a specific message.
    /// </summary>
    public async Task<Guid?> GetSenderIdByMessageIdAsync(long messageId)
    {
        return await _chatRepository.GetSenderIdByMessageIdAsync(messageId);
    }

    /// <summary>
    /// Retrieves delivery statuses for all recipients of a message.
    /// </summary>
    public async Task<IEnumerable<MessageStatusDto>> GetMessageStatusAsync(long messageId)
    {
        return await _chatRepository.GetMessageStatusAsync(messageId);
    }

    /// <summary>
    /// Gets the conversation ID associated with a specific message.
    /// </summary>
    public async Task<Guid> GetConversationIdByMessageIdAsync(long messageId)
    {
        return await _chatRepository.GetConversationIdByMessageIdAsync(messageId);
    }

    /// <summary>
    /// Retrieves all member IDs for a specific conversation.
    /// </summary>
    public async Task<List<Guid>> GetConversationMembersAsync(Guid conversationId)
    {
        return await _chatRepository.GetConversationMembersAsync(conversationId);
    }

    // ========================================
    // MESSAGE ACTIONS
    // ========================================

    /// <summary>
    /// Deletes a message for the current user or for all participants.
    /// Returns error message if deletion fails, null on success.
    /// </summary>
    public async Task<string?> DeleteMessageAsync(long messageId, Guid userId, bool deleteForEveryone)
    {
        return await _chatRepository.DeleteMessageAsync(messageId, userId, deleteForEveryone);
    }

    /// <summary>
    /// Edits the content of an existing message.
    /// Returns error message if edit fails, null on success.
    /// </summary>
    public async Task<string?> EditMessageAsync(long messageId, Guid userId, string newBody)
    {
        if (string.IsNullOrWhiteSpace(newBody))
        {
            return "Message cannot be empty";
        }

        return await _chatRepository.EditMessageAsync(messageId, userId, newBody);
    }

    /// <summary>
    /// Forwards a message to another conversation.
    /// Returns the new message ID on success, or error message on failure.
    /// </summary>
    public async Task<(long? MessageId, string? ErrorMessage)> ForwardMessageAsync(
        long originalMessageId,
        Guid forwardedBy,
        Guid targetConversationId)
    {
        return await _chatRepository.ForwardMessageAsync(originalMessageId, forwardedBy, targetConversationId);
    }

    // ========================================
    // GROUP MANAGEMENT
    // ========================================

    /// <summary>
    /// Creates a new group chat with specified members.
    /// Validates input parameters and ensures creator is included in members.
    /// </summary>
    public async Task<(Guid? ConversationId, string? ErrorMessage)> CreateGroupAsync(
        Guid creatorUserId,
        string groupName,
        string? groupPhotoUrl,
        List<Guid> memberUserIds)
    {
        // Validate group name
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return (null, "Group name is required");
        }

        if (groupName.Length > 100)
        {
            return (null, "Group name must be less than 100 characters");
        }

        // Validate members
        if (memberUserIds == null || memberUserIds.Count == 0)
        {
            return (null, "At least one member is required");
        }

        // Ensure creator is included in members
        if (!memberUserIds.Contains(creatorUserId))
        {
            memberUserIds.Add(creatorUserId);
        }

        return await _chatRepository.CreateGroupAsync(creatorUserId, groupName, groupPhotoUrl, memberUserIds);
    }

    /// <summary>
    /// Retrieves detailed information about a specific group.
    /// Returns null if group doesn't exist or user is not a member.
    /// </summary>
    public async Task<GroupDetailsDto?> GetGroupDetailsAsync(Guid conversationId, Guid userId)
    {
        return await _chatRepository.GetGroupDetailsAsync(conversationId, userId);
    }

    /// <summary>
    /// Adds a new member to an existing group.
    /// Returns error message if addition fails, null on success.
    /// </summary>
    public async Task<string?> AddGroupMemberAsync(Guid conversationId, Guid userId, Guid addedBy)
    {
        return await _chatRepository.AddGroupMemberAsync(conversationId, userId, addedBy);
    }

    /// <summary>
    /// Removes a member from a group.
    /// Returns error message if removal fails, null on success.
    /// </summary>
    public async Task<string?> RemoveGroupMemberAsync(Guid conversationId, Guid userId, Guid removedBy)
    {
        return await _chatRepository.RemoveGroupMemberAsync(conversationId, userId, removedBy);
    }

    /// <summary>
    /// Allows a user to leave a group.
    /// Returns error message if operation fails, null on success.
    /// </summary>
    public async Task<string?> LeaveGroupAsync(Guid conversationId, Guid userId)
    {
        return await _chatRepository.LeaveGroupAsync(conversationId, userId);
    }

    /// <summary>
    /// Checks if a user is an administrator of a group.
    /// </summary>
    public async Task<bool> IsUserAdminAsync(Guid conversationId, Guid userId)
    {
        return await _chatRepository.IsUserAdminAsync(conversationId, userId);
    }

    /// <summary>
    /// Transfers group administration from one user to another.
    /// Validates that new admin is a group member.
    /// </summary>
    public async Task<string?> TransferAdminAsync(Guid conversationId, Guid oldAdminId, Guid newAdminId)
    {
        // Verify new admin is a member
        var groupDetails = await _chatRepository.GetGroupDetailsAsync(conversationId, oldAdminId);
        if (groupDetails == null || !groupDetails.Members.Any(m => m.UserId == newAdminId))
        {
            return "New admin must be a group member";
        }

        return await _chatRepository.TransferAdminAsync(conversationId, oldAdminId, newAdminId);
    }

    /// <summary>
    /// Updates group information (name and/or photo).
    /// Validates input parameters before updating.
    /// </summary>
    public async Task<string?> UpdateGroupInfoAsync(
        Guid conversationId,
        Guid userId,
        string? groupName,
        string? groupPhotoUrl)
    {
        // Validate group name
        if (groupName != null && string.IsNullOrWhiteSpace(groupName))
        {
            return "Group name cannot be empty";
        }

        if (groupName != null && groupName.Length > 100)
        {
            return "Group name must be less than 100 characters";
        }

        return await _chatRepository.UpdateGroupInfoAsync(conversationId, userId, groupName, groupPhotoUrl);
    }

    public async Task<SearchResponseDto> SearchMessagesAsync(
    Guid userId,
    string query,
    Guid? senderId,
    Guid? conversationId,
    DateTime? startDate,
    DateTime? endDate,
    int page,
    int pageSize)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchResponseDto(
                Enumerable.Empty<SearchResultDto>(),
                0,
                page,
                pageSize
            );
        }

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        var (results, totalCount) = await _chatRepository.SearchMessagesAsync(
            userId,
            query,
            senderId,
            conversationId,
            startDate,
            endDate,
            page,
            pageSize
        );

        return new SearchResponseDto(results, totalCount, page, pageSize);
    }

    /// <summary>
    /// Permanently deletes a group.
    /// Returns error message if deletion fails, null on success.
    /// </summary>
    public async Task<string?> DeleteGroupAsync(Guid conversationId, Guid userId)
    {
        return await _groupRepository.DeleteGroupAsync(conversationId, userId);
    }
}