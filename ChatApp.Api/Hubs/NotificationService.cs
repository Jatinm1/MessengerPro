using ChatApp.Api.Hubs;
using ChatApp.Application.DTOs.Chat;
using ChatApp.Application.DTOs.Group;
using ChatApp.Application.Interfaces.IServices;
using Microsoft.AspNetCore.SignalR;

namespace ChatApp.Infrastructure.Services;

/// <summary>
/// Service for sending real-time notifications to clients via SignalR.
/// Handles broadcasting of chat events, group updates, and message status changes.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IHubContext<ChatHub> _hubContext;

    /// <summary>
    /// Initializes a new instance of NotificationService with SignalR hub context.
    /// </summary>
    public NotificationService(IHubContext<ChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Notifies recipients about a new message and confirms delivery to sender.
    /// </summary>
    public async Task NotifyMessageSentAsync(MessageSentDto message)
    {
        var payload = new
        {
            messageId = message.MessageId,
            conversationId = message.ConversationId,
            fromUserId = message.FromUserId,
            fromUserName = message.FromUserName,
            fromDisplayName = message.FromDisplayName,
            body = message.Body,
            contentType = message.ContentType,
            mediaUrl = message.MediaUrl,
            createdAtUtc = message.CreatedAtUtc,
            messageStatus = message.MessageStatus
        };

        // Send to all recipients
        foreach (var recipientId in message.RecipientIds)
        {
            await _hubContext.Clients.Group($"user:{recipientId}")
                .SendAsync("messageReceived", payload);
        }

        // Confirm delivery to sender
        await _hubContext.Clients.Group($"user:{message.FromUserId}")
            .SendAsync("messageSent", payload);
    }

    /// <summary>
    /// Broadcasts message status updates (sent/delivered/read) to all connected clients.
    /// </summary>
    public async Task NotifyMessageStatusUpdatedAsync(long messageId, Guid conversationId, string status)
    {
        await _hubContext.Clients.All.SendAsync("messageStatusUpdated", new
        {
            messageId,
            conversationId,
            status
        });
    }

    /// <summary>
    /// Notifies all members when a new group is created.
    /// </summary>
    public async Task NotifyGroupCreatedAsync(GroupDetailsDto groupDetails)
    {
        foreach (var member in groupDetails.Members)
        {
            await _hubContext.Clients.Group($"user:{member.UserId}")
                .SendAsync("groupCreated", groupDetails);
        }
    }

    /// <summary>
    /// Notifies when a new member is added to a group.
    /// Sends specific notification to added user and general update to other members.
    /// </summary>
    public async Task NotifyGroupMemberAddedAsync(Guid conversationId, Guid addedUserId, Guid addedBy)
    {
        // Notify the newly added user
        await _hubContext.Clients.Group($"user:{addedUserId}")
            .SendAsync("addedToGroup", new { conversationId, addedBy });

        // Notify existing group members
        await _hubContext.Clients.Group($"conversation:{conversationId}")
            .SendAsync("groupMemberAdded", new { conversationId, addedUserId, addedBy });
    }

    /// <summary>
    /// Notifies when a member is removed from a group.
    /// Sends specific notification to removed user and general update to remaining members.
    /// </summary>
    public async Task NotifyGroupMemberRemovedAsync(Guid conversationId, Guid removedUserId, Guid removedBy)
    {
        // Notify the removed user
        await _hubContext.Clients.Group($"user:{removedUserId}")
            .SendAsync("removedFromGroup", new { conversationId, removedBy });

        // Notify remaining group members
        await _hubContext.Clients.Group($"conversation:{conversationId}")
            .SendAsync("groupMemberRemoved", new { conversationId, removedUserId, removedBy });
    }

    /// <summary>
    /// Notifies all members when a group is permanently deleted.
    /// </summary>
    public async Task NotifyGroupDeletedAsync(Guid conversationId, string groupName, Guid deletedBy, List<Guid> memberIds)
    {
        foreach (var memberId in memberIds)
        {
            await _hubContext.Clients.Group($"user:{memberId}")
                .SendAsync("groupDeleted", new
                {
                    conversationId,
                    groupName,
                    deletedBy
                });
        }
    }

    /// <summary>
    /// Notifies relevant users when a message is deleted.
    /// Handles both "delete for me" and "delete for everyone" scenarios.
    /// </summary>
    public async Task NotifyMessageDeletedAsync(long messageId, Guid conversationId, Guid deletedBy, bool deleteForEveryone, List<Guid> memberIds)
    {
        foreach (var memberId in memberIds)
        {
            await _hubContext.Clients.Group($"user:{memberId}")
                .SendAsync("messageDeleted", new
                {
                    messageId,
                    conversationId,
                    deletedBy,
                    deleteForEveryone
                });
        }
    }

    /// <summary>
    /// Notifies group members when a message is edited.
    /// </summary>
    public async Task NotifyMessageEditedAsync(long messageId, Guid conversationId, string newBody, Guid editedBy, List<Guid> memberIds)
    {
        foreach (var memberId in memberIds)
        {
            await _hubContext.Clients.Group($"user:{memberId}")
                .SendAsync("messageEdited", new
                {
                    messageId,
                    conversationId,
                    newBody,
                    editedBy,
                    editedAtUtc = DateTime.UtcNow
                });
        }
    }
}