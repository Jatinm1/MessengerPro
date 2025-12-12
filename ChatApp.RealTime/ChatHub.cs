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

namespace ChatApp.RealTime
{

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


        public async Task NotifyGroupLeft(Guid conversationId)
        {
            var userId = CurrentUserId;

            // This client (the leaver) should remove the group immediately
            await Clients.Group($"user:{userId}").SendAsync("groupLeft", new
            {
                conversationId = conversationId
            });

            Console.WriteLine($"[Group] User {userId} left group {conversationId}");
        }


        // ========================================
        // FRIEND REQUEST REAL-TIME
        // ========================================

        // ========================================
        // FRIEND REQUEST REAL-TIME - UPDATED
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

            // ✅ Get the actual request that was created/updated
            var requests = await _friendService.GetReceivedRequestsAsync(receiverId);
            var newRequest = requests.FirstOrDefault(r => r.SenderId == senderId && r.Status == "Pending");

            if (newRequest != null)
            {
                // ✅ Notify receiver about the new/updated request
                await Clients.Group($"user:{receiverId}").SendAsync("friendRequestReceived", newRequest);

                // ✅ Update receiver's received requests list
                var receiverRequests = await _friendService.GetReceivedRequestsAsync(receiverId);
                await Clients.Group($"user:{receiverId}").SendAsync("receivedRequestsUpdated", receiverRequests);
            }

            // ✅ Notify sender with updated status
            await Clients.Caller.SendAsync("friendRequestSent", new { receiverId });

            // ✅ Update sender's sent requests list
            var senderRequests = await _friendService.GetSentRequestsAsync(senderId);
            await Clients.Caller.SendAsync("sentRequestsUpdated", senderRequests);

            Console.WriteLine($"✅ [FriendRequest] {sender?.UserName} → {receiverId} - All clients notified");
        }

        public async Task AcceptFriendRequest(long requestId)
        {
            var userId = CurrentUserId;

            // ✅ Get request details BEFORE accepting
            var requests = await _friendService.GetReceivedRequestsAsync(userId);
            var requestToAccept = requests.FirstOrDefault(r => r.RequestId == requestId);

            if (requestToAccept == null)
            {
                await Clients.Caller.SendAsync("friendRequestError", "Request not found");
                return;
            }

            var senderId = requestToAccept.SenderId;
            var receiverId = userId;

            var (success, errorMessage) = await _friendService.AcceptFriendRequestAsync(requestId, userId);

            if (!success)
            {
                await Clients.Caller.SendAsync("friendRequestError", errorMessage);
                return;
            }

            Console.WriteLine($"✅ [FriendRequest] {receiverId} accepted request from {senderId}");

            var acceptedByUser = await _users.GetByIdAsync(receiverId);

            // ========================================
            // NOTIFY SENDER (person who sent the request)
            // ========================================

            // 1. Tell sender their request was accepted
            await Clients.Group($"user:{senderId}").SendAsync("friendRequestAccepted", new
            {
                requestId,
                acceptedBy = receiverId,
                acceptedByName = acceptedByUser?.DisplayName ?? "Someone"
            });

            // 2. Update sender's sent requests list (request should be removed or marked accepted)
            var senderSentRequests = await _friendService.GetSentRequestsAsync(senderId);
            await Clients.Group($"user:{senderId}").SendAsync("sentRequestsUpdated", senderSentRequests);

            // 3. Update sender's friends list (new friend added)
            var senderFriends = await _friendService.GetFriendsListAsync(senderId);
            await Clients.Group($"user:{senderId}").SendAsync("friendsListUpdated", senderFriends);

            // ========================================
            // NOTIFY RECEIVER (person who accepted)
            // ========================================

            // 4. Confirm to receiver that they accepted the request
            await Clients.Caller.SendAsync("friendRequestAcceptedConfirm", new { requestId });

            // 5. Update receiver's received requests list (request should be removed)
            var receiverReceivedRequests = await _friendService.GetReceivedRequestsAsync(receiverId);
            await Clients.Caller.SendAsync("receivedRequestsUpdated", receiverReceivedRequests);

            // 6. Update receiver's friends list (new friend added)
            var receiverFriends = await _friendService.GetFriendsListAsync(receiverId);
            await Clients.Caller.SendAsync("friendsListUpdated", receiverFriends);

            Console.WriteLine($"✅ [FriendRequest] Acceptance complete - Sender and Receiver both notified with full updates");
        }

        public async Task RejectFriendRequest(long requestId)
        {
            var userId = CurrentUserId;

            // ✅ Get request details BEFORE rejecting
            var requests = await _friendService.GetReceivedRequestsAsync(userId);
            var requestToReject = requests.FirstOrDefault(r => r.RequestId == requestId);

            if (requestToReject == null)
            {
                await Clients.Caller.SendAsync("friendRequestError", "Request not found");
                return;
            }

            var senderId = requestToReject.SenderId;
            var receiverId = userId;

            var (success, errorMessage) = await _friendService.RejectFriendRequestAsync(requestId, userId);

            if (!success)
            {
                await Clients.Caller.SendAsync("friendRequestError", errorMessage);
                return;
            }

            Console.WriteLine($"❌ [FriendRequest] {receiverId} rejected request from {senderId}");

            // ========================================
            // NOTIFY SENDER (person whose request was rejected)
            // ========================================

            // 1. Tell sender their request was rejected
            await Clients.Group($"user:{senderId}").SendAsync("friendRequestRejected", new
            {
                requestId,
                rejectedBy = receiverId
            });

            // 2. Update sender's sent requests list (mark as rejected)
            var senderSentRequests = await _friendService.GetSentRequestsAsync(senderId);
            await Clients.Group($"user:{senderId}").SendAsync("sentRequestsUpdated", senderSentRequests);

            // ========================================
            // NOTIFY RECEIVER (person who rejected)
            // ========================================

            // 3. Confirm to receiver that they rejected the request
            await Clients.Caller.SendAsync("friendRequestRejectedConfirm", new { requestId });

            // 4. Update receiver's received requests list (request should be removed)
            var receiverReceivedRequests = await _friendService.GetReceivedRequestsAsync(receiverId);
            await Clients.Caller.SendAsync("receivedRequestsUpdated", receiverReceivedRequests);

            Console.WriteLine($"❌ [FriendRequest] Rejection complete - Sender and Receiver both notified with full updates");
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

        public async Task DeleteGroup(Guid conversationId)
        {
            var userId = CurrentUserId;

            // ✅ Fetch group BEFORE deleting
            var groupDetails = await _chat.GetGroupDetailsAsync(conversationId, userId);
            if (groupDetails == null)
            {
                await Clients.Caller.SendAsync("groupError", "Group not found.");
                return;
            }

            // ❗ Delete AFTER fetching details
            var errorMessage = await _chat.DeleteGroupAsync(conversationId, userId);
            if (errorMessage != null)
            {
                await Clients.Caller.SendAsync("groupError", errorMessage);
                return;
            }

            // 🔥 Now broadcast to all members
            foreach (var member in groupDetails.Members)
            {
                await Clients.Group($"user:{member.UserId}")
                    .SendAsync("groupDeleted", new
                    {
                        conversationId = conversationId,
                        groupName = groupDetails.GroupName,
                        deletedBy = userId
                    });
            }

            Console.WriteLine($"[Group] Group {conversationId} deleted by {userId}");
        }

        // Add to ChatApp.Api/SignalR/ChatHub.cs

        public async Task DeleteMessage(long messageId, bool deleteForEveryone)
        {
            var userId = CurrentUserId;

            var error = await _chat.DeleteMessageAsync(messageId, userId, deleteForEveryone);

            if (error != null)
            {
                await Clients.Caller.SendAsync("messageActionError", error);
                return;
            }

            var conversationId = await _chat.GetConversationIdByMessageIdAsync(messageId);
            var members = await _chat.GetConversationMembersAsync(conversationId);

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

            Console.WriteLine($"[Message] Deleted: {messageId} by {userId} (deleteForEveryone: {deleteForEveryone})");
        }

        public async Task EditMessage(long messageId, string newBody)
        {
            var userId = CurrentUserId;

            var error = await _chat.EditMessageAsync(messageId, userId, newBody);

            if (error != null)
            {
                await Clients.Caller.SendAsync("messageActionError", error);
                return;
            }

            var conversationId = await _chat.GetConversationIdByMessageIdAsync(messageId);
            var members = await _chat.GetConversationMembersAsync(conversationId);

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

            Console.WriteLine($"[Message] Edited: {messageId} by {userId}");
        }

        public async Task ForwardMessage(long originalMessageId, Guid targetConversationId)
        {
            var userId = CurrentUserId;

            var (newMessageId, error) = await _chat.ForwardMessageAsync(originalMessageId, userId, targetConversationId);

            if (error != null)
            {
                await Clients.Caller.SendAsync("messageActionError", error);
                return;
            }

            // Get message details and notify recipients
            var groupDetails = await _chat.GetGroupDetailsAsync(targetConversationId, userId);
            var sender = await _users.GetByIdAsync(userId);

            // Get the forwarded message content
            var messages = await _chat.GetChatHistoryAsync(targetConversationId, userId, 1, 1);
            var forwardedMessage = messages.FirstOrDefault();

            if (forwardedMessage != null)
            {
                var payload = new
                {
                    MessageId = forwardedMessage.MessageId,
                    ConversationId = targetConversationId,
                    FromUserId = userId,
                    FromUserName = sender?.UserName ?? "Unknown",
                    FromDisplayName = sender?.DisplayName ?? "Unknown",
                    Body = forwardedMessage.Body,
                    ContentType = forwardedMessage.ContentType,
                    MediaUrl = forwardedMessage.MediaUrl,
                    CreatedAtUtc = forwardedMessage.CreatedAtUtc,
                    MessageStatus = "Sent"
                };

                // Notify all members
                if (groupDetails != null)
                {
                    foreach (var member in groupDetails.Members.Where(m => m.UserId != userId))
                    {
                        await Clients.Group($"user:{member.UserId}").SendAsync("messageReceived", payload);
                    }
                }
                else
                {
                    // Direct conversation
                    var members = await _chat.GetConversationMembersAsync(targetConversationId);
                    foreach (var memberId in members.Where(m => m != userId))
                    {
                        await Clients.Group($"user:{memberId}").SendAsync("messageReceived", payload);
                    }
                }

                await Clients.Caller.SendAsync("messageSent", payload);
            }

            Console.WriteLine($"[Message] Forwarded: {originalMessageId} to {targetConversationId} by {userId}");
        }

    }
}