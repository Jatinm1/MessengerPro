// ========================================
// ChatApp.Api/Hubs/ChatHub.cs
// ========================================
using ChatApp.Application.Interfaces.IRepositories;
using ChatApp.Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace ChatApp.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    // Track online users and their connection IDs
    private static readonly ConcurrentDictionary<Guid, HashSet<string>> _userConnections = new();

    private readonly IChatService _chatService;
    private readonly IUserRepository _userRepository;
    private readonly IFriendService _friendService;
    private readonly IUserService _userService;
    private readonly IGroupService _groupService;
    // =======================
    // ACTIVE CALL TRACKING
    // =======================
    private static readonly ConcurrentDictionary<string, (Guid Caller, Guid Callee)>
        _activeCalls = new();


    public ChatHub(
        IChatService chatService,
        IUserRepository userRepository,
        IFriendService friendService,
        IUserService userService,
        IGroupService groupService)
    {
        _chatService = chatService;
        _userRepository = userRepository;
        _friendService = friendService;
        _userService = userService;
        _groupService = groupService;
    }

    private Guid CurrentUserId => Guid.Parse(
        Context.User!.FindFirstValue(ClaimTypes.NameIdentifier) ??
        Context.User!.FindFirstValue("sub")!);

    // ========================================
    // CONNECTION MANAGEMENT
    // ========================================

    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId;
        var connectionId = Context.ConnectionId;

        // Add connection to tracking
        var connections = _userConnections.GetOrAdd(userId, _ => new HashSet<string>());
        lock (connections)
        {
            connections.Add(connectionId);
        }

        // Add to user-specific group
        await Groups.AddToGroupAsync(connectionId, $"user:{userId}");

        // Update online status
        await _userService.UpdateUserOnlineStatusAsync(userId, true);

        // Notify friends that user is online
        await NotifyFriendsOnlineStatusAsync(userId, true);

        Console.WriteLine($"✅ [Connected] User {userId} | Connection {connectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = CurrentUserId;
        var connectionId = Context.ConnectionId;

        // Remove connection from tracking
        if (_userConnections.TryGetValue(userId, out var connections))
        {
            lock (connections)
            {
                connections.Remove(connectionId);
            }

            // If no more connections, mark user as offline
            if (connections.Count == 0)
            {
                _userConnections.TryRemove(userId, out _);
                await _userService.UpdateUserOnlineStatusAsync(userId, false);
                await NotifyFriendsOnlineStatusAsync(userId, false);
            }
        }

        await Groups.RemoveFromGroupAsync(connectionId, $"user:{userId}");

        Console.WriteLine($"❌ [Disconnected] User {userId} | Connection {connectionId}");
        await base.OnDisconnectedAsync(exception);
    }

    private async Task NotifyFriendsOnlineStatusAsync(Guid userId, bool isOnline)
    {
        var friends = await _friendService.GetFriendsListAsync(userId);
        var user = await _userRepository.GetByIdAsync(userId);

        foreach (var friend in friends)
        {
            await Clients.Group($"user:{friend.FriendUserId}")
                .SendAsync("userStatusChanged", new
                {
                    userId = userId,
                    userName = user?.UserName,
                    displayName = user?.DisplayName,
                    isOnline = isOnline,
                    lastSeenUtc = isOnline ? (DateTime?)null : DateTime.UtcNow
                });
        }
    }

    // ========================================
    // DIRECT MESSAGING
    // ========================================

    public async Task SendDirect(Guid toUserId, string body, string? contentType = "text", string? mediaUrl = null)
    {
        var fromUserId = CurrentUserId;

        // Validate friendship
        var areFriends = await _friendService.AreFriendsAsync(fromUserId, toUserId);
        if (!areFriends)
        {
            await Clients.Caller.SendAsync("error", "You can only message friends");
            return;
        }

        // Validate message
        if (string.IsNullOrWhiteSpace(body) && string.IsNullOrWhiteSpace(mediaUrl))
        {
            await Clients.Caller.SendAsync("error", "Message cannot be empty");
            return;
        }

        // Get or create conversation
        var conversationId = await _chatService.GetOrCreateConversationAsync(fromUserId, toUserId);

        // Save message
        var messageId = await _chatService.SaveMessageAsync(
            conversationId,
            fromUserId,
            body ?? mediaUrl ?? "",
            contentType ?? "text",
            mediaUrl);

        // Get sender details
        var sender = await _userRepository.GetByIdAsync(fromUserId);

        var payload = new
        {
            messageId = messageId,
            conversationId = conversationId,
            fromUserId = fromUserId,
            fromUserName = sender?.UserName ?? "Unknown",
            fromDisplayName = sender?.DisplayName ?? "Unknown",
            body = body,
            contentType = contentType ?? "text",
            mediaUrl = mediaUrl,
            createdAtUtc = DateTime.UtcNow,
            messageStatus = "Sent"
        };

        // Send to receiver
        await Clients.Group($"user:{toUserId}").SendAsync("messageReceived", payload);

        // Auto-mark as delivered if receiver is online
        if (_userConnections.ContainsKey(toUserId))
        {
            await _chatService.UpdateMessageStatusAsync(messageId, toUserId, "Delivered");

            await Clients.Group($"user:{fromUserId}").SendAsync("messageStatusUpdated", new
            {
                messageId = messageId,
                conversationId = conversationId,
                status = "Delivered"
            });
        }

        // Confirm to sender
        await Clients.Caller.SendAsync("messageSent", payload);

        Console.WriteLine($"📨 [DirectMessage] {sender?.UserName} → {toUserId}: {body}");
    }

    // ========================================
    // GROUP MESSAGING
    // ========================================

    public async Task SendGroupMessage(Guid conversationId, string body, string? contentType = "text", string? mediaUrl = null)
    {
        var senderId = CurrentUserId;

        // Validate membership
        var groupDetails = await _groupService.GetGroupDetailsAsync(conversationId, senderId);
        if (groupDetails == null)
        {
            await Clients.Caller.SendAsync("error", "You are not a member of this group");
            return;
        }

        // Validate message
        if (string.IsNullOrWhiteSpace(body) && string.IsNullOrWhiteSpace(mediaUrl))
        {
            await Clients.Caller.SendAsync("error", "Message cannot be empty");
            return;
        }

        // Save message
        var messageId = await _chatService.SaveMessageAsync(
            conversationId,
            senderId,
            body ?? mediaUrl ?? "",
            contentType ?? "text",
            mediaUrl);

        // Get sender details
        var sender = await _userRepository.GetByIdAsync(senderId);

        var payload = new
        {
            messageId = messageId,
            conversationId = conversationId,
            fromUserId = senderId,
            fromUserName = sender?.UserName ?? "Unknown",
            fromDisplayName = sender?.DisplayName ?? "Unknown",
            body = body,
            contentType = contentType ?? "text",
            mediaUrl = mediaUrl,
            createdAtUtc = DateTime.UtcNow,
            messageStatus = "Sent"
        };

        // Send to all group members except sender
        foreach (var member in groupDetails.Members.Where(m => m.UserId != senderId))
        {
            await Clients.Group($"user:{member.UserId}").SendAsync("messageReceived", payload);

            // Auto-mark as delivered if member is online
            if (_userConnections.ContainsKey(member.UserId))
            {
                await _chatService.UpdateMessageStatusAsync(messageId, member.UserId, "Delivered");
            }
        }

        // Confirm to sender
        await Clients.Caller.SendAsync("messageSent", payload);

        Console.WriteLine($"📨 [GroupMessage] {sender?.UserName} → Group {groupDetails.GroupName}: {body}");
    }

    // ========================================
    // MESSAGE STATUS UPDATES
    // ========================================

    public async Task MarkMessageDelivered(long messageId)
    {
        var userId = CurrentUserId;
        await _chatService.UpdateMessageStatusAsync(messageId, userId, "Delivered");

        await Clients.All.SendAsync("messageStatusUpdated", new
        {
            messageId = messageId,
            userId = userId,
            status = "Delivered"
        });
    }

    public async Task MarkMessageRead(long messageId)
    {
        var userId = CurrentUserId;

        // Update status
        await _chatService.UpdateMessageStatusAsync(messageId, userId, "Read");

        // Get sender ID
        var senderId = await _chatService.GetSenderIdByMessageIdAsync(messageId);
        if (senderId == null || senderId == userId)
            return;

        // Notify sender
        await Clients.Group($"user:{senderId}").SendAsync("messageStatusUpdated", new
        {
            messageId = messageId,
            status = "Read",
            readBy = userId
        });

        // Notify reader's other devices
        await Clients.Group($"user:{userId}").SendAsync("messageStatusUpdated", new
        {
            messageId = messageId,
            status = "Read"
        });

        Console.WriteLine($"✅ [Read] User {userId} read message {messageId}");
    }

    public async Task MarkConversationRead(Guid conversationId, long lastReadMessageId)
    {
        var userId = CurrentUserId;

        // Update all messages as read
        var affectedSenders = await _chatService.MarkMessagesAsReadAsync(
            conversationId,
            userId,
            lastReadMessageId);

        // Notify all affected senders
        foreach (var senderId in affectedSenders)
        {
            await Clients.Group($"user:{senderId}").SendAsync("conversationReadUpdated", new
            {
                conversationId = conversationId,
                userId = userId,
                lastReadMessageId = lastReadMessageId
            });
        }

        // Notify reader's other devices
        await Clients.Group($"user:{userId}").SendAsync("conversationMarkedAsRead", new
        {
            conversationId = conversationId,
            lastReadMessageId = lastReadMessageId
        });

        Console.WriteLine($"✅ [ConversationRead] User {userId} marked conversation {conversationId} as read");
    }

    // ========================================
    // GROUP MANAGEMENT
    // ========================================

    public async Task CreateGroup(string groupName, string? groupPhotoUrl, List<Guid> memberUserIds)
    {
        var creatorId = CurrentUserId;

        // Validate
        if (string.IsNullOrWhiteSpace(groupName))
        {
            await Clients.Caller.SendAsync("groupCreationError", "Group name is required");
            return;
        }

        if (memberUserIds == null || memberUserIds.Count == 0)
        {
            await Clients.Caller.SendAsync("groupCreationError", "At least one member is required");
            return;
        }

        // Add creator if not included
        if (!memberUserIds.Contains(creatorId))
        {
            memberUserIds.Add(creatorId);
        }

        // Create group
        var (conversationId, errorMessage) = await _groupService.CreateGroupAsync(
            creatorId,
            groupName,
            groupPhotoUrl,
            memberUserIds);

        if (errorMessage != null)
        {
            await Clients.Caller.SendAsync("groupCreationError", errorMessage);
            return;
        }

        // Get full group details
        var groupDetails = await _groupService.GetGroupDetailsAsync(conversationId!.Value, creatorId);

        // Notify all members
        foreach (var member in groupDetails!.Members)
        {
            await Clients.Group($"user:{member.UserId}").SendAsync("groupCreated", groupDetails);
        }

        Console.WriteLine($"🎉 [Group] Created: {groupName} by {creatorId}");
    }

    public async Task AddMemberToGroup(Guid conversationId, Guid userId)
    {
        var addedBy = CurrentUserId;

        // Verify admin permissions
        var isAdmin = await _chatService.IsUserAdminAsync(conversationId, addedBy);
        if (!isAdmin)
        {
            await Clients.Caller.SendAsync("groupError", "Only admins can add members");
            return;
        }

        var errorMessage = await _groupService.AddGroupMemberAsync(conversationId, userId, addedBy);

        if (errorMessage != null)
        {
            await Clients.Caller.SendAsync("groupError", errorMessage);
            return;
        }

        // Get updated group details
        var groupDetails = await _groupService.GetGroupDetailsAsync(conversationId, addedBy);

        // Notify all members
        foreach (var member in groupDetails!.Members)
        {
            await Clients.Group($"user:{member.UserId}").SendAsync("groupMemberAdded", new
            {
                conversationId = conversationId,
                addedUserId = userId,
                addedBy = addedBy,
                groupDetails = groupDetails
            });
        }

        Console.WriteLine($"➕ [Group] User {userId} added to {conversationId} by {addedBy}");
    }

    public async Task RemoveMemberFromGroup(Guid conversationId, Guid userId)
    {
        var removedBy = CurrentUserId;

        // Verify admin permissions
        var isAdmin = await _chatService.IsUserAdminAsync(conversationId, removedBy);
        if (!isAdmin)
        {
            await Clients.Caller.SendAsync("groupError", "Only admins can remove members");
            return;
        }

        // Cannot remove yourself (use LeaveGroup instead)
        if (userId == removedBy)
        {
            await Clients.Caller.SendAsync("groupError", "Use leave group to remove yourself");
            return;
        }

        var errorMessage = await _groupService.RemoveGroupMemberAsync(conversationId, userId, removedBy);

        if (errorMessage != null)
        {
            await Clients.Caller.SendAsync("groupError", errorMessage);
            return;
        }

        // Notify the removed member
        await Clients.Group($"user:{userId}").SendAsync("removedFromGroup", new
        {
            conversationId = conversationId,
            removedBy = removedBy
        });

        // Get updated group details
        var groupDetails = await _groupService.GetGroupDetailsAsync(conversationId, removedBy);

        // Notify remaining members
        if (groupDetails != null)
        {
            foreach (var member in groupDetails.Members)
            {
                await Clients.Group($"user:{member.UserId}").SendAsync("groupMemberRemoved", new
                {
                    conversationId = conversationId,
                    removedUserId = userId,
                    removedBy = removedBy,
                    groupDetails = groupDetails
                });
            }
        }

        Console.WriteLine($"➖ [Group] User {userId} removed from {conversationId} by {removedBy}");
    }

    public async Task LeaveGroup(Guid conversationId)
    {
        var userId = CurrentUserId;

        var errorMessage = await _chatService.LeaveGroupAsync(conversationId, userId);

        if (errorMessage != null)
        {
            await Clients.Caller.SendAsync("groupError", errorMessage);
            return;
        }

        // Notify user they've left
        await Clients.Group($"user:{userId}").SendAsync("groupLeft", new
        {
            conversationId = conversationId
        });

        Console.WriteLine($"👋 [Group] User {userId} left group {conversationId}");
    }

    public async Task UpdateGroupInfo(Guid conversationId, string? groupName, string? groupPhotoUrl)
    {
        var userId = CurrentUserId;

        // Verify admin permissions
        var isAdmin = await _chatService.IsUserAdminAsync(conversationId, userId);
        if (!isAdmin)
        {
            await Clients.Caller.SendAsync("groupError", "Only admins can update group info");
            return;
        }

        var errorMessage = await _groupService.UpdateGroupInfoAsync(
            conversationId,
            userId,
            groupName,
            groupPhotoUrl);

        if (errorMessage != null)
        {
            await Clients.Caller.SendAsync("groupError", errorMessage);
            return;
        }

        // Get updated group details
        var groupDetails = await _groupService.GetGroupDetailsAsync(conversationId, userId);

        // Notify all members
        if (groupDetails != null)
        {
            foreach (var member in groupDetails.Members)
            {
                await Clients.Group($"user:{member.UserId}").SendAsync("groupInfoUpdated", groupDetails);
            }
        }

        Console.WriteLine($"✏️ [Group] Info updated for {conversationId} by {userId}");
    }

    public async Task TransferAdmin(Guid conversationId, Guid newAdminId)
    {
        var oldAdminId = CurrentUserId;

        // Verify current user is admin
        var isAdmin = await _chatService.IsUserAdminAsync(conversationId, oldAdminId);
        if (!isAdmin)
        {
            await Clients.Caller.SendAsync("groupError", "Only admins can transfer admin rights");
            return;
        }

        var errorMessage = await _chatService.TransferAdminAsync(conversationId, oldAdminId, newAdminId);

        if (errorMessage != null)
        {
            await Clients.Caller.SendAsync("groupError", errorMessage);
            return;
        }

        // Get updated group details
        var groupDetails = await _groupService.GetGroupDetailsAsync(conversationId, oldAdminId);

        // Notify all members
        if (groupDetails != null)
        {
            foreach (var member in groupDetails.Members)
            {
                await Clients.Group($"user:{member.UserId}").SendAsync("adminTransferred", new
                {
                    conversationId = conversationId,
                    oldAdminId = oldAdminId,
                    newAdminId = newAdminId,
                    groupDetails = groupDetails
                });
            }
        }

        Console.WriteLine($"👑 [Group] Admin transferred from {oldAdminId} to {newAdminId}");
    }

    public async Task DeleteGroup(Guid conversationId)
    {
        var userId = CurrentUserId;

        // Verify admin permissions
        var isAdmin = await _chatService.IsUserAdminAsync(conversationId, userId);
        if (!isAdmin)
        {
            await Clients.Caller.SendAsync("groupError", "Only admins can delete the group");
            return;
        }

        // Get group details BEFORE deletion
        var groupDetails = await _groupService.GetGroupDetailsAsync(conversationId, userId);
        if (groupDetails == null)
        {
            await Clients.Caller.SendAsync("groupError", "Group not found");
            return;
        }

        // Delete group
        var errorMessage = await _groupService.DeleteGroupAsync(conversationId, userId);

        if (errorMessage != null)
        {
            await Clients.Caller.SendAsync("groupError", errorMessage);
            return;
        }

        // Notify all members
        foreach (var member in groupDetails.Members)
        {
            await Clients.Group($"user:{member.UserId}").SendAsync("groupDeleted", new
            {
                conversationId = conversationId,
                groupName = groupDetails.GroupName,
                deletedBy = userId
            });
        }

        Console.WriteLine($"🗑️ [Group] Group {conversationId} deleted by {userId}");
    }

    // ========================================
    // MESSAGE ACTIONS (Delete, Edit, Forward)
    // ========================================

    public async Task DeleteMessage(long messageId, bool deleteForEveryone)
    {
        var userId = CurrentUserId;

        var result = await _chatService.DeleteMessageAsync(messageId, userId, deleteForEveryone);

        if (!string.IsNullOrEmpty(result))
        {
            await Clients.Caller.SendAsync("messageActionError", result);
            return;
        }

        var conversationId = await _chatService.GetConversationIdByMessageIdAsync(messageId);
        var members = await _chatService.GetConversationMembersAsync(conversationId);

        // Notify all members
        foreach (var memberId in members)
        {
            await Clients.Group($"user:{memberId}").SendAsync("messageDeleted", new
            {
                messageId = messageId,
                conversationId = conversationId,
                deletedBy = userId,
                deleteForEveryone = deleteForEveryone
            });
        }

        Console.WriteLine($"🗑️ [Message] Deleted: {messageId} by {userId} (deleteForEveryone: {deleteForEveryone})");
    }

    public async Task EditMessage(long messageId, string newBody)
    {
        var userId = CurrentUserId;

        if (string.IsNullOrWhiteSpace(newBody))
        {
            await Clients.Caller.SendAsync("messageActionError", "Message cannot be empty");
            return;
        }

        var result = await _chatService.EditMessageAsync(messageId, userId, newBody);

        if (!string.IsNullOrEmpty(result))
        {
            await Clients.Caller.SendAsync("messageActionError", result);
            return;
        }

        var conversationId = await _chatService.GetConversationIdByMessageIdAsync(messageId);
        var members = await _chatService.GetConversationMembersAsync(conversationId);

        // Notify all members
        foreach (var memberId in members)
        {
            await Clients.Group($"user:{memberId}").SendAsync("messageEdited", new
            {
                messageId = messageId,
                conversationId = conversationId,
                newBody = newBody,
                editedBy = userId,
                editedAtUtc = DateTime.UtcNow
            });
        }

        Console.WriteLine($"✏️ [Message] Edited: {messageId} by {userId}");
    }

    public async Task ForwardMessage(long originalMessageId, Guid targetConversationId)
    {
        var userId = CurrentUserId;

        var (newMessageId, errorMessage) = await _chatService.ForwardMessageAsync(
            originalMessageId,
            userId,
            targetConversationId);

        if (errorMessage != null || newMessageId == null)
        {
            await Clients.Caller.SendAsync("messageActionError", errorMessage ?? "Failed to forward message");
            return;
        }

        // Get message details
        var messages = await _chatService.GetChatHistoryAsync(targetConversationId, userId, 1, 1);
        var forwardedMessage = messages.FirstOrDefault();

        if (forwardedMessage != null)
        {
            var sender = await _userRepository.GetByIdAsync(userId);

            var payload = new
            {
                messageId = forwardedMessage.MessageId,
                conversationId = targetConversationId,
                fromUserId = userId,
                fromUserName = sender?.UserName ?? "Unknown",
                fromDisplayName = sender?.DisplayName ?? "Unknown",
                body = forwardedMessage.Body,
                contentType = forwardedMessage.ContentType,
                mediaUrl = forwardedMessage.MediaUrl,
                createdAtUtc = forwardedMessage.CreatedAtUtc,
                messageStatus = "Sent"
            };

            // Notify recipients
            var members = await _chatService.GetConversationMembersAsync(targetConversationId);
            foreach (var memberId in members.Where(m => m != userId))
            {
                await Clients.Group($"user:{memberId}").SendAsync("messageReceived", payload);
            }

            await Clients.Caller.SendAsync("messageSent", payload);
        }

        Console.WriteLine($"↪️ [Message] Forwarded: {originalMessageId} to {targetConversationId} by {userId}");
    }

    // ========================================
    // FRIEND REQUESTS (Real-time)
    // ========================================

    public async Task SendFriendRequest(Guid receiverId)
    {
        var senderId = CurrentUserId;

        var (success, errorMessage) = await _friendService.SendFriendRequestAsync(senderId, receiverId);

        if (!success)
        {
            await Clients.Caller.SendAsync("friendRequestError", errorMessage);
            return;
        }

        var sender = await _userRepository.GetByIdAsync(senderId);

        // Get the actual request that was created/updated
        var receivedRequests = await _friendService.GetReceivedRequestsAsync(receiverId);
        var newRequest = receivedRequests.FirstOrDefault(r => r.SenderId == senderId && r.Status == "Pending");

        if (newRequest != null)
        {
            // Notify receiver
            await Clients.Group($"user:{receiverId}").SendAsync("friendRequestReceived", newRequest);

            // Update receiver's received requests list
            await Clients.Group($"user:{receiverId}").SendAsync("receivedRequestsUpdated", receivedRequests);
        }

        // Notify sender
        await Clients.Caller.SendAsync("friendRequestSent", new { receiverId });

        // Update sender's sent requests list
        var sentRequests = await _friendService.GetSentRequestsAsync(senderId);
        await Clients.Caller.SendAsync("sentRequestsUpdated", sentRequests);

        Console.WriteLine($"🤝 [FriendRequest] {sender?.UserName} → {receiverId}");
    }

    public async Task AcceptFriendRequest(long requestId)
    {
        var userId = CurrentUserId;

        // Get request details BEFORE accepting
        var requests = await _friendService.GetReceivedRequestsAsync(userId);
        var requestToAccept = requests.FirstOrDefault(r => r.RequestId == requestId);

        if (requestToAccept == null)
        {
            await Clients.Caller.SendAsync("friendRequestError", "Request not found");
            return;
        }

        var senderId = requestToAccept.SenderId;

        var (success, errorMessage) = await _friendService.AcceptFriendRequestAsync(requestId, userId);

        if (!success)
        {
            await Clients.Caller.SendAsync("friendRequestError", errorMessage);
            return;
        }

        var acceptedByUser = await _userRepository.GetByIdAsync(userId);

        // Notify sender
        await Clients.Group($"user:{senderId}").SendAsync("friendRequestAccepted", new
        {
            requestId = requestId,
            acceptedBy = userId,
            acceptedByName = acceptedByUser?.DisplayName ?? "Someone"
        });

        // Update sender's lists
        var senderSentRequests = await _friendService.GetSentRequestsAsync(senderId);
        await Clients.Group($"user:{senderId}").SendAsync("sentRequestsUpdated", senderSentRequests);

        var senderFriends = await _friendService.GetFriendsListAsync(senderId);
        await Clients.Group($"user:{senderId}").SendAsync("friendsListUpdated", senderFriends);

        // Notify receiver (confirmer)
        await Clients.Caller.SendAsync("friendRequestAcceptedConfirm", new { requestId });

        // Update receiver's lists
        var receiverReceivedRequests = await _friendService.GetReceivedRequestsAsync(userId);
        await Clients.Caller.SendAsync("receivedRequestsUpdated", receiverReceivedRequests);

        var receiverFriends = await _friendService.GetFriendsListAsync(userId);
        await Clients.Caller.SendAsync("friendsListUpdated", receiverFriends);

        Console.WriteLine($"✅ [FriendRequest] Accepted: Request {requestId} by {userId}");
    }

    public async Task RejectFriendRequest(long requestId)
    {
        var userId = CurrentUserId;

        // Get request details BEFORE rejecting
        var requests = await _friendService.GetReceivedRequestsAsync(userId);
        var requestToReject = requests.FirstOrDefault(r => r.RequestId == requestId);

        if (requestToReject == null)
        {
            await Clients.Caller.SendAsync("friendRequestError", "Request not found");
            return;
        }

        var senderId = requestToReject.SenderId;

        var (success, errorMessage) = await _friendService.RejectFriendRequestAsync(requestId, userId);

        if (!success)
        {
            await Clients.Caller.SendAsync("friendRequestError", errorMessage);
            return;
        }

        // Notify sender
        await Clients.Group($"user:{senderId}").SendAsync("friendRequestRejected", new
        {
            requestId = requestId,
            rejectedBy = userId
        });

        // Update sender's sent requests
        var senderSentRequests = await _friendService.GetSentRequestsAsync(senderId);
        await Clients.Group($"user:{senderId}").SendAsync("sentRequestsUpdated", senderSentRequests);

        // Notify receiver (rejecter)
        await Clients.Caller.SendAsync("friendRequestRejectedConfirm", new { requestId });

        // Update receiver's received requests
        var receiverReceivedRequests = await _friendService.GetReceivedRequestsAsync(userId);
        await Clients.Caller.SendAsync("receivedRequestsUpdated", receiverReceivedRequests);

        Console.WriteLine($"❌ [FriendRequest] Rejected: Request {requestId} by {userId}");
    }

    // Add these methods to your existing ChatHub.cs file

    // ========================================
    // WEBRTC CALLING METHODS
    // ========================================

    /// <summary>
    /// Send a call offer to initiate a call
    /// </summary>
    public async Task SendCallOffer(
        Guid conversationId,
        Guid recipientId,
        string callType,
        object sdp)
    {
        var callerId = CurrentUserId;
        var callId = $"call_{Guid.NewGuid()}";

        // Get caller info
        var caller = await _userRepository.GetByIdAsync(callerId);

        // Check if recipient is online
        if (!IsUserOnline(recipientId))
        {
            await Clients.Caller.SendAsync("callBusy", new { callId });
            return;
        }

        // Check if recipient is already in a call (you can add a separate tracking for this)
        // For simplicity, we'll send the offer

        var offer = new
        {
            callId = callId,
            conversationId = conversationId,
            callType = callType,
            from = new
            {
                userId = callerId,
                userName = caller?.UserName ?? "Unknown",
                displayName = caller?.DisplayName ?? "Unknown",
                photoUrl = caller?.ProfilePhotoUrl
            },
            to = new
            {
                userId = recipientId
            },
            sdp = sdp
        };

        // Send offer to recipient
        await Clients.Group($"user:{recipientId}").SendAsync("calloffer", offer);

        Console.WriteLine($"📞 [Call] Offer sent from {caller?.UserName} to {recipientId}");
    }

    /// <summary>
    /// Send a call answer to accept a call
    /// </summary>
    public async Task SendCallAnswer(string callId, object sdp)
    {
        var userId = CurrentUserId;

        var answer = new
        {
            callId = callId,
            sdp = sdp
        };

        // Send answer to all connections (caller will receive it)
        await Clients.Others.SendAsync("callanswer", answer);

        Console.WriteLine($"✅ [Call] Answer sent for call {callId} by {userId}");
    }

    /// <summary>
    /// Send ICE candidate for WebRTC connection
    /// </summary>
    public async Task SendIceCandidate(string callId, object candidate)
    {
        var userId = CurrentUserId;

        var iceCandidate = new
        {
            callId = callId,
            candidate = candidate
        };

        // Send to all other participants
        await Clients.Others.SendAsync("icecandidate", iceCandidate);

        Console.WriteLine($"🧊 [Call] ICE candidate sent for call {callId}");
    }

    /// <summary>
    /// Reject an incoming call
    /// </summary>
    public async Task RejectCall(string callId, string reason)
    {
        var userId = CurrentUserId;

        await Clients.Others.SendAsync("callrejected", new
        {
            callId = callId,
            reason = reason,
            rejectedBy = userId
        });

        Console.WriteLine($"❌ [Call] Call {callId} rejected by {userId}");
    }

    /// <summary>
    /// End an active call
    /// </summary>
    public async Task EndCall(string callId, string reason)
    {
        var userId = CurrentUserId;

        await Clients.Others.SendAsync("callended", new
        {
            callId = callId,
            endedBy = userId,
            reason = reason
        });

        Console.WriteLine($"📴 [Call] Call {callId} ended by {userId}");
    }

    /// <summary>
    /// Send call state update (mute, video off, screen share)
    /// </summary>
    public async Task SendCallStateUpdate(string callId, object stateUpdate)
    {
        var userId = CurrentUserId;

        // Create a dynamic object with the state update
        var update = new
        {
            callId = callId,
            userId = userId,
            isMuted = (stateUpdate as dynamic)?.isMuted,
            isVideoOff = (stateUpdate as dynamic)?.isVideoOff,
            isScreenSharing = (stateUpdate as dynamic)?.isScreenSharing
        };

        await Clients.Others.SendAsync("callstateupdate", update);

        Console.WriteLine($"🔄 [Call] State update sent for call {callId} by {userId}");
    }

    /// <summary>
    /// Send busy signal when user is already in a call
    /// </summary>
    public async Task SendBusySignal(string callId, Guid recipientId)
    {
        await Clients.Group($"user:{recipientId}").SendAsync("callbusy", new
        {
            callId = callId
        });

        Console.WriteLine($"📵 [Call] Busy signal sent for call {callId}");
    }

    // ========================================
    // UTILITY METHODS
    // ========================================

    public static bool IsUserOnline(Guid userId)
    {
        return _userConnections.ContainsKey(userId);
    }

    public static int GetConnectionCount(Guid userId)
    {
        if (_userConnections.TryGetValue(userId, out var connections))
        {
            return connections.Count;
        }
        return 0;
    }

    // ========================================
    // CALL HELPERS
    // ========================================
    private Guid GetOtherParticipant(string callId)
    {
        if (!_activeCalls.TryGetValue(callId, out var call))
            throw new HubException("Call not found");

        return call.Caller == CurrentUserId
            ? call.Callee
            : call.Caller;
    }

}