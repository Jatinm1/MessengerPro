// ========================================
// ChatApp.Api/SignalR/ChatHub.cs
// ========================================
using ChatApp.Application.Services;
using ChatApp.Domain.Chat;
using ChatApp.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace ChatApp.Api.SignalR;

[Authorize]
public class ChatHub : Hub
{
    private static readonly ConcurrentDictionary<Guid, HashSet<string>> _userConnections = new();
    private readonly IChatService _chat;
    private readonly IUserRepository _users;
    private readonly IFriendService _friendService;
    private readonly IUserService _userService;

    public ChatHub(IChatService chat, IUserRepository users, IFriendService friendService, IUserService userService)
    {
        _chat = chat;
        _users = users;
        _friendService = friendService;
        _userService = userService;
    }

    private Guid CurrentUserId =>
        Guid.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Context.User!.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    public override async Task OnConnectedAsync()
    {
        var uid = CurrentUserId;
        var set = _userConnections.GetOrAdd(uid, _ => new HashSet<string>());
        lock (set) set.Add(Context.ConnectionId);

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{uid}");

        // Update user online status
        await _userService.UpdateUserOnlineStatusAsync(uid, true);

        // Notify friends that user is online
        await NotifyFriendsOnlineStatus(uid, true);

        Console.WriteLine($"[Connected] {uid} joined group user:{uid}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var uid = CurrentUserId;
        if (_userConnections.TryGetValue(uid, out var set))
        {
            lock (set) set.Remove(Context.ConnectionId);
            if (set.Count == 0)
            {
                _userConnections.TryRemove(uid, out _);

                // Update user offline status and last seen
                await _userService.UpdateUserOnlineStatusAsync(uid, false);

                // Notify friends that user is offline
                await NotifyFriendsOnlineStatus(uid, false);
            }
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{uid}");
        Console.WriteLine($"[Disconnected] {uid} left group user:{uid}");
        await base.OnDisconnectedAsync(exception);
    }

    private async Task NotifyFriendsOnlineStatus(Guid userId, bool isOnline)
    {
        var friends = await _friendService.GetFriendsListAsync(userId);
        var user = await _users.GetByIdAsync(userId);

        foreach (var friend in friends)
        {
            await Clients.Group($"user:{friend.FriendUserId}").SendAsync("userStatusChanged", new
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

    public async Task SendDirect(Guid toUserId, string body)
    {
        var fromUserId = CurrentUserId;

        // Check friendship
        if (!await _friendService.AreFriendsAsync(fromUserId, toUserId))
        {
            await Clients.Caller.SendAsync("error", "You can only message friends");
            return;
        }

        // Save message
        var (convId, msgId) = await _chat.SendDirectAsync(fromUserId, toUserId, body);
        var fromUser = await _users.GetByIdAsync(fromUserId);

        var payload = new
        {
            MessageId = msgId,
            ConversationId = convId,
            FromUserId = fromUserId,
            FromUserName = fromUser?.UserName ?? "Unknown",
            FromDisplayName = fromUser?.DisplayName ?? "Unknown",
            Body = body,
            CreatedAtUtc = DateTime.UtcNow,
            MessageStatus = "Sent"
        };

        // ✅ Send to receiver
        await Clients.Group($"user:{toUserId}").SendAsync("messageReceived", payload);

        // ✅ If receiver is online → mark Delivered automatically
        if (_userConnections.ContainsKey(toUserId))
        {
            await _chat.UpdateMessageStatusAsync(msgId, toUserId, "Delivered");

            await Clients.Group($"user:{fromUserId}").SendAsync("messageStatusUpdated", new
            {
                messageId = msgId,
                conversationId = convId,
                status = "Delivered"
            });
        }

        // ✅ Sender confirmation
        await Clients.Caller.SendAsync("messageSent", payload);

        Console.WriteLine($"[Message] {fromUser?.UserName} → {toUserId}: {body}");
    }


    // ========================================
    // GROUP MESSAGING
    // ========================================

    public async Task SendGroupMessage(Guid conversationId, string body)
    {
        var senderId = CurrentUserId;
        var sender = await _users.GetByIdAsync(senderId);

        // Get group details to verify membership
        var groupDetails = await _chat.GetGroupDetailsAsync(conversationId, senderId);
        if (groupDetails == null)
        {
            await Clients.Caller.SendAsync("error", "You are not a member of this group");
            return;
        }

        // Save message
        var (convId, msgId) = await _chat.SendGroupMessageAsync(conversationId, senderId, body);

        var payload = new
        {
            MessageId = msgId,
            ConversationId = conversationId,
            FromUserId = senderId,
            FromUserName = sender?.UserName ?? "Unknown",
            FromDisplayName = sender?.DisplayName ?? "Unknown",
            Body = body,
            CreatedAtUtc = DateTime.UtcNow,
            MessageStatus = "Sent"
        };

        // Send to all group members except sender
        foreach (var member in groupDetails.Members.Where(m => m.UserId != senderId))
        {
            await Clients.Group($"user:{member.UserId}").SendAsync("messageReceived", payload);

            // Auto-mark as delivered if member is online
            if (_userConnections.ContainsKey(member.UserId))
            {
                await _chat.UpdateMessageStatusAsync(msgId, member.UserId, "Delivered");
            }
        }

        // Send confirmation to sender
        await Clients.Caller.SendAsync("messageSent", payload);

        Console.WriteLine($"[GroupMessage] {sender?.UserName} → Group {groupDetails.GroupName}: {body}");
    }

    // ========================================
    // MESSAGE STATUS UPDATES
    // ========================================

    public async Task MarkMessageDelivered(long messageId)
    {
        var userId = CurrentUserId;
        await _chat.UpdateMessageStatusAsync(messageId, userId, "Delivered");

        // Notify sender
        var statuses = await _chat.GetMessageStatusAsync(messageId);
        var firstStatus = statuses.FirstOrDefault();
        if (firstStatus != null)
        {
            // Get the message to find sender
            // Note: You might need to add a method to get message by ID
            // For now, we'll notify through a general update
            await Clients.All.SendAsync("messageStatusUpdated", new
            {
                messageId = messageId,
                userId = userId,
                status = "Delivered"
            });
        }
    }

    public async Task MarkMessageRead(long messageId)
    {
        var readerId = CurrentUserId;

        // ✅ 1. Update DB (set message as Read)
        await _chat.UpdateMessageStatusAsync(messageId, readerId, "Read");

        // ✅ 2. Get original sender
        var senderId = await _chat.GetSenderIdByMessageIdAsync(messageId);
        if (senderId == null || senderId == readerId)
            return; // safety check (don't notify self)

        // ✅ 3. Notify only the sender in real-time
        await Clients.Group($"user:{senderId}").SendAsync("messageStatusUpdated", new
        {
            messageId = messageId,
            status = "Read"
        });

        // ✅ 4. Optionally notify reader's other devices (if multi-device support)
        await Clients.Group($"user:{readerId}").SendAsync("messageStatusUpdated", new
        {
            messageId = messageId,
            status = "Read"
        });

        Console.WriteLine($"[Read] User {readerId} read message {messageId} from {senderId}");
    }


    public async Task MarkConversationRead(Guid conversationId, long lastReadMessageId)
    {
        var userId = CurrentUserId;

        // Update all messages up to lastReadMessageId as read
        var affectedSenders = await _chat.MarkMessagesAsReadAsync(conversationId, userId, lastReadMessageId);

        // ✅ Notify all affected senders about the read status
        foreach (var senderId in affectedSenders)
        {
            await Clients.Group($"user:{senderId}").SendAsync("conversationReadUpdated", new
            {
                conversationId = conversationId,
                userId = userId,
                lastReadMessageId = lastReadMessageId
            });
        }

        // ✅ Notify reader's other devices
        await Clients.Group($"user:{userId}").SendAsync("conversationMarkedAsRead", new
        {
            conversationId = conversationId,
            lastReadMessageId = lastReadMessageId
        });

        Console.WriteLine($"[ConversationRead] User {userId} marked conversation {conversationId} as read up to message {lastReadMessageId}");
    }

    // ========================================
    // GROUP MANAGEMENT
    // ========================================

    public async Task CreateGroup(string groupName, string? groupPhotoUrl, List<Guid> memberUserIds)
    {
        var creatorId = CurrentUserId;

        var (conversationId, errorMessage) = await _chat.CreateGroupAsync(
            creatorId,
            groupName,
            groupPhotoUrl,
            memberUserIds
        );

        if (errorMessage != null)
        {
            await Clients.Caller.SendAsync("groupCreationError", errorMessage);
            return;
        }

        // Get full group details
        var groupDetails = await _chat.GetGroupDetailsAsync(conversationId!.Value, creatorId);

        // Notify all members about the new group
        foreach (var member in groupDetails!.Members)
        {
            await Clients.Group($"user:{member.UserId}").SendAsync("groupCreated", groupDetails);
        }

        Console.WriteLine($"[Group] Created: {groupName} by {creatorId}");
    }

    public async Task AddMemberToGroup(Guid conversationId, Guid userId)
    {
        var addedBy = CurrentUserId;
        var errorMessage = await _chat.AddGroupMemberAsync(conversationId, userId, addedBy);

        if (errorMessage != null)
        {
            await Clients.Caller.SendAsync("groupError", errorMessage);
            return;
        }

        // Get updated group details
        var groupDetails = await _chat.GetGroupDetailsAsync(conversationId, addedBy);

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

        Console.WriteLine($"[Group] User {userId} added to {conversationId} by {addedBy}");
    }

    public async Task RemoveMemberFromGroup(Guid conversationId, Guid userId)
    {
        var removedBy = CurrentUserId;
        var errorMessage = await _chat.RemoveGroupMemberAsync(conversationId, userId, removedBy);

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
        var groupDetails = await _chat.GetGroupDetailsAsync(conversationId, removedBy);

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

        Console.WriteLine($"[Group] User {userId} removed from {conversationId} by {removedBy}");
    }

    public async Task UpdateGroupInfo(Guid conversationId, string? groupName, string? groupPhotoUrl)
    {
        var userId = CurrentUserId;
        var errorMessage = await _chat.UpdateGroupInfoAsync(conversationId, userId, groupName, groupPhotoUrl);

        if (errorMessage != null)
        {
            await Clients.Caller.SendAsync("groupError", errorMessage);
            return;
        }

        // Get updated group details
        var groupDetails = await _chat.GetGroupDetailsAsync(conversationId, userId);

        // Notify all members
        if (groupDetails != null)
        {
            foreach (var member in groupDetails.Members)
            {
                await Clients.Group($"user:{member.UserId}").SendAsync("groupInfoUpdated", groupDetails);
            }
        }

        Console.WriteLine($"[Group] Info updated for {conversationId} by {userId}");
    }

    // ========================================
    // FRIEND REQUEST REAL-TIME
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

        var sender = await _users.GetByIdAsync(senderId);
        var requests = await _friendService.GetReceivedRequestsAsync(receiverId);
        var newRequest = requests.FirstOrDefault(r => r.SenderId == senderId && r.Status == "Pending");

        if (newRequest != null)
        {
            await Clients.Group($"user:{receiverId}").SendAsync("friendRequestReceived", newRequest);
        }

        await Clients.Caller.SendAsync("friendRequestSent", new { receiverId });
        Console.WriteLine($"[FriendRequest] {sender?.UserName} → {receiverId}");
    }

    public async Task AcceptFriendRequest(long requestId)
    {
        var userId = CurrentUserId;
        var (success, errorMessage) = await _friendService.AcceptFriendRequestAsync(requestId, userId);

        if (!success)
        {
            await Clients.Caller.SendAsync("friendRequestError", errorMessage);
            return;
        }

        var requests = await _friendService.GetReceivedRequestsAsync(userId);
        var acceptedRequest = requests.FirstOrDefault(r => r.RequestId == requestId);

        if (acceptedRequest != null)
        {
            var senderId = acceptedRequest.SenderId;
            var receiverId = userId;

            await Clients.Group($"user:{senderId}").SendAsync("friendRequestAccepted", new
            {
                requestId,
                acceptedBy = receiverId,
                acceptedByName = (await _users.GetByIdAsync(receiverId))?.DisplayName
            });

            await Clients.Caller.SendAsync("friendRequestAcceptedConfirm", new { requestId });

            var senderFriends = await _friendService.GetFriendsListAsync(senderId);
            var receiverFriends = await _friendService.GetFriendsListAsync(receiverId);

            await Clients.Group($"user:{senderId}").SendAsync("friendsListUpdated", senderFriends);
            await Clients.Group($"user:{receiverId}").SendAsync("friendsListUpdated", receiverFriends);

            Console.WriteLine($"[FriendRequest] {receiverId} accepted request from {senderId}");
        }
    }

    public async Task RejectFriendRequest(long requestId)
    {
        var userId = CurrentUserId;
        var (success, errorMessage) = await _friendService.RejectFriendRequestAsync(requestId, userId);

        if (!success)
        {
            await Clients.Caller.SendAsync("friendRequestError", errorMessage);
            return;
        }

        var requests = await _friendService.GetReceivedRequestsAsync(userId);
        var rejectedRequest = requests.FirstOrDefault(r => r.RequestId == requestId);

        if (rejectedRequest != null)
        {
            var senderId = rejectedRequest.SenderId;

            await Clients.Group($"user:{senderId}").SendAsync("friendRequestRejected", new
            {
                requestId,
                rejectedBy = userId
            });

            await Clients.Caller.SendAsync("friendRequestRejectedConfirm", new { requestId });
            Console.WriteLine($"[FriendRequest] {userId} rejected request from {senderId}");
        }
    }

    // Add these methods to your existing ChatHub

    public async Task SendDirectMedia(Guid toUserId, string mediaUrl, string mediaType, string? caption)
    {
        var fromUserId = CurrentUserId;

        // Check friendship
        if (!await _friendService.AreFriendsAsync(fromUserId, toUserId))
        {
            await Clients.Caller.SendAsync("error", "You can only message friends");
            return;
        }

        // Save message with media
        var body = caption ?? mediaUrl;
        var (convId, msgId) = await _chat.SendDirectAsync(fromUserId, toUserId, body, mediaType, mediaUrl);
        var fromUser = await _users.GetByIdAsync(fromUserId);

        var payload = new
        {
            MessageId = msgId,
            ConversationId = convId,
            FromUserId = fromUserId,
            FromUserName = fromUser?.UserName ?? "Unknown",
            FromDisplayName = fromUser?.DisplayName ?? "Unknown",
            Body = body,
            ContentType = mediaType,
            MediaUrl = mediaUrl,
            CreatedAtUtc = DateTime.UtcNow,
            MessageStatus = "Sent"
        };

        // Send to receiver
        await Clients.Group($"user:{toUserId}").SendAsync("messageReceived", payload);

        // Auto-mark as delivered if receiver is online
        if (_userConnections.ContainsKey(toUserId))
        {
            await _chat.UpdateMessageStatusAsync(msgId, toUserId, "Delivered");

            await Clients.Group($"user:{fromUserId}").SendAsync("messageStatusUpdated", new
            {
                messageId = msgId,
                conversationId = convId,
                status = "Delivered"
            });
        }

        // Sender confirmation
        await Clients.Caller.SendAsync("messageSent", payload);

        Console.WriteLine($"[Media] {fromUser?.UserName} → {toUserId}: {mediaType}");
    }

    public async Task SendGroupMedia(Guid conversationId, string mediaUrl, string mediaType, string? caption)
    {
        var senderId = CurrentUserId;
        var sender = await _users.GetByIdAsync(senderId);

        // Get group details to verify membership
        var groupDetails = await _chat.GetGroupDetailsAsync(conversationId, senderId);
        if (groupDetails == null)
        {
            await Clients.Caller.SendAsync("error", "You are not a member of this group");
            return;
        }

        // Save message with media
        var body = caption ?? mediaUrl;
        var (convId, msgId) = await _chat.SendGroupMessageAsync(conversationId, senderId, body, mediaType, mediaUrl);

        var payload = new
        {
            MessageId = msgId,
            ConversationId = conversationId,
            FromUserId = senderId,
            FromUserName = sender?.UserName ?? "Unknown",
            FromDisplayName = sender?.DisplayName ?? "Unknown",
            Body = body,
            ContentType = mediaType,
            MediaUrl = mediaUrl,
            CreatedAtUtc = DateTime.UtcNow,
            MessageStatus = "Sent"
        };

        // Send to all group members except sender
        foreach (var member in groupDetails.Members.Where(m => m.UserId != senderId))
        {
            await Clients.Group($"user:{member.UserId}").SendAsync("messageReceived", payload);

            // Auto-mark as delivered if member is online
            if (_userConnections.ContainsKey(member.UserId))
            {
                await _chat.UpdateMessageStatusAsync(msgId, member.UserId, "Delivered");
            }
        }

        // Send confirmation to sender
        await Clients.Caller.SendAsync("messageSent", payload);

        Console.WriteLine($"[GroupMedia] {sender?.UserName} → Group {groupDetails.GroupName}: {mediaType}");
    }
}