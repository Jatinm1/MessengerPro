// ========================================
// ChatApp.Api/Hubs/CallHub.cs
// WebRTC signaling hub for audio calls
// ========================================
using ChatApp.Application.Interfaces.IRepositories;
using ChatApp.Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace ChatApp.Api.Hubs;

[Authorize]
public class CallHub : Hub
{
    // Active calls: callId -> CallSession
    private static readonly ConcurrentDictionary<string, ActiveCallSession> _activeCalls = new();

    // User -> active callId (prevent multiple simultaneous calls)
    private static readonly ConcurrentDictionary<Guid, string> _userActiveCall = new();

    private readonly IUserRepository _userRepository;
    private readonly IFriendService _friendService;

    public CallHub(IUserRepository userRepository, IFriendService friendService)
    {
        _userRepository = userRepository;
        _friendService = friendService;
    }

    private Guid CurrentUserId => Guid.Parse(
        Context.User!.FindFirstValue(ClaimTypes.NameIdentifier) ??
        Context.User!.FindFirstValue("sub")!);

    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"call-user:{userId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = CurrentUserId;

        // Auto-end any active call on disconnect
        if (_userActiveCall.TryGetValue(userId, out var callId))
        {
            await EndCallInternal(callId, userId, "disconnected");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"call-user:{userId}");
        await base.OnDisconnectedAsync(exception);
    }

    // ========================================
    // INITIATE CALL
    // ========================================

    /// <summary>
    /// Called by the caller to initiate a WebRTC audio call.
    /// Sends ring notification to callee with caller info.
    /// </summary>
    public async Task InitiateCall(Guid toUserId, string callId, string conversationId)
    {
        var fromUserId = CurrentUserId;

        // Validate friendship
        var areFriends = await _friendService.AreFriendsAsync(fromUserId, toUserId);
        if (!areFriends)
        {
            await Clients.Caller.SendAsync("callError", new { callId, message = "You can only call friends" });
            return;
        }

        // Check if caller already in a call
        if (_userActiveCall.ContainsKey(fromUserId))
        {
            await Clients.Caller.SendAsync("callError", new { callId, message = "You are already in a call" });
            return;
        }

        // Check if callee is busy
        if (_userActiveCall.ContainsKey(toUserId))
        {
            await Clients.Caller.SendAsync("calleeBusy", new { callId, toUserId });
            return;
        }

        var caller = await _userRepository.GetByIdAsync(fromUserId);

        // Register call session
        var session = new ActiveCallSession
        {
            CallId = callId,
            ConversationId = conversationId,
            CallerId = fromUserId,
            CalleeId = toUserId,
            Status = "ringing",
            StartedAt = DateTime.UtcNow
        };

        _activeCalls[callId] = session;
        _userActiveCall[fromUserId] = callId;

        // Notify callee
        await Clients.Group($"call-user:{toUserId}").SendAsync("incomingCall", new
        {
            callId,
            conversationId,
            callerId = fromUserId,
            callerName = caller?.DisplayName ?? caller?.UserName ?? "Unknown",
            callerPhoto = caller?.ProfilePhotoUrl,
            callType = "audio"
        });

        Console.WriteLine($"📞 [Call] Initiated: {callId} | {fromUserId} → {toUserId}");
    }

    // ========================================
    // ANSWER / DECLINE
    // ========================================

    /// <summary>
    /// Called by callee to accept the incoming call.
    /// </summary>
    public async Task AnswerCall(string callId)
    {
        var calleeId = CurrentUserId;

        if (!_activeCalls.TryGetValue(callId, out var session))
        {
            await Clients.Caller.SendAsync("callError", new { callId, message = "Call not found or already ended" });
            return;
        }

        if (session.CalleeId != calleeId)
        {
            await Clients.Caller.SendAsync("callError", new { callId, message = "Unauthorized" });
            return;
        }

        session.Status = "connecting";
        _userActiveCall[calleeId] = callId;

        // Notify caller that callee accepted
        await Clients.Group($"call-user:{session.CallerId}").SendAsync("callAnswered", new { callId });

        Console.WriteLine($"✅ [Call] Answered: {callId}");
    }

    /// <summary>
    /// Called by callee to decline the incoming call.
    /// </summary>
    public async Task DeclineCall(string callId)
    {
        var userId = CurrentUserId;

        if (!_activeCalls.TryGetValue(callId, out var session))
            return;

        await EndCallInternal(callId, userId, "declined");
        Console.WriteLine($"❌ [Call] Declined: {callId}");
    }

    // ========================================
    // WEBRTC SIGNALING (SDP + ICE)
    // ========================================

    /// <summary>
    /// Forward SDP offer from caller to callee.
    /// </summary>
    public async Task SendOffer(string callId, string sdp)
    {
        var fromUserId = CurrentUserId;

        if (!_activeCalls.TryGetValue(callId, out var session))
            return;

        var targetId = session.CallerId == fromUserId ? session.CalleeId : session.CallerId;

        await Clients.Group($"call-user:{targetId}").SendAsync("receiveOffer", new { callId, sdp });
    }

    /// <summary>
    /// Forward SDP answer from callee to caller.
    /// </summary>
    public async Task SendAnswer(string callId, string sdp)
    {
        var fromUserId = CurrentUserId;

        if (!_activeCalls.TryGetValue(callId, out var session))
            return;

        var targetId = session.CallerId == fromUserId ? session.CalleeId : session.CallerId;

        await Clients.Group($"call-user:{targetId}").SendAsync("receiveAnswer", new { callId, sdp });
        session.Status = "connected";
        session.ConnectedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Forward ICE candidate to the other peer.
    /// </summary>
    public async Task SendIceCandidate(string callId, string candidateJson)
    {
        var fromUserId = CurrentUserId;

        if (!_activeCalls.TryGetValue(callId, out var session))
            return;

        var targetId = session.CallerId == fromUserId ? session.CalleeId : session.CallerId;

        await Clients.Group($"call-user:{targetId}").SendAsync("receiveIceCandidate", new
        {
            callId,
            candidate = candidateJson
        });
    }

    // ========================================
    // CALL CONTROL (Mute)
    // ========================================

    /// <summary>
    /// Notify the other peer of local mute state change.
    /// </summary>
    public async Task UpdateCallState(string callId, bool isMuted)
    {
        var userId = CurrentUserId;

        if (!_activeCalls.TryGetValue(callId, out var session))
            return;

        var targetId = session.CallerId == userId ? session.CalleeId : session.CallerId;

        await Clients.Group($"call-user:{targetId}").SendAsync("peerStateChanged", new
        {
            callId,
            userId,
            isMuted
        });
    }

    // ========================================
    // END CALL
    // ========================================

    /// <summary>
    /// End the call — notifies the other peer and cleans up state.
    /// </summary>
    public async Task EndCall(string callId)
    {
        var userId = CurrentUserId;
        await EndCallInternal(callId, userId, "ended");
        Console.WriteLine($"📵 [Call] Ended: {callId} by {userId}");
    }

    // ========================================
    // PRIVATE HELPERS
    // ========================================

    private async Task EndCallInternal(string callId, Guid initiatedBy, string reason)
    {
        if (!_activeCalls.TryRemove(callId, out var session))
            return;

        // Clean up user->call mappings
        _userActiveCall.TryRemove(session.CallerId, out _);
        _userActiveCall.TryRemove(session.CalleeId, out _);

        var duration = session.ConnectedAt.HasValue
            ? (int)(DateTime.UtcNow - session.ConnectedAt.Value).TotalSeconds
            : 0;

        var payload = new
        {
            callId,
            reason,
            initiatedBy,
            durationSeconds = duration
        };

        // Notify both participants
        await Clients.Group($"call-user:{session.CallerId}").SendAsync("callEnded", payload);
        await Clients.Group($"call-user:{session.CalleeId}").SendAsync("callEnded", payload);
    }

    // ========================================
    // STATIC HELPERS (used by ChatHub)
    // ========================================

    public static bool IsUserInCall(Guid userId) => _userActiveCall.ContainsKey(userId);
}

// ========================================
// Supporting model (internal to hub layer)
// ========================================
public class ActiveCallSession
{
    public string CallId { get; set; } = "";
    public string ConversationId { get; set; } = "";
    public Guid CallerId { get; set; }
    public Guid CalleeId { get; set; }
    public string Status { get; set; } = "ringing"; // ringing | connecting | connected
    public DateTime StartedAt { get; set; }
    public DateTime? ConnectedAt { get; set; }
}