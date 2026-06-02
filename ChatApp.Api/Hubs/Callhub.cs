// ============================================================
// ChatApp.Api/Hubs/CallHub.cs
// WebRTC signaling hub — Audio + Video + Screen Share
// ============================================================
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
    // callId → session
    private static readonly ConcurrentDictionary<string, ActiveCallSession> _activeCalls = new();

    // userId → callId  (prevents double-calls)
    private static readonly ConcurrentDictionary<Guid, string> _userActiveCall = new();

    private readonly IUserRepository _userRepository;
    private readonly IFriendService _friendService;
    private readonly ICallRepository _callRepository;

    public CallHub(
        IUserRepository userRepository,
        IFriendService friendService,
        ICallRepository callRepository)
    {
        _userRepository = userRepository;
        _friendService = friendService;
        _callRepository = callRepository;
    }

    // ── Convenience ─────────────────────────────────────────────────────────
    private Guid CurrentUserId => Guid.Parse(
        Context.User!.FindFirstValue(ClaimTypes.NameIdentifier) ??
        Context.User!.FindFirstValue("sub")!);

    // ── Connection lifecycle ─────────────────────────────────────────────────
    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId;
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = CurrentUserId;

        if (_userActiveCall.TryGetValue(userId, out var callId))
        {
            // Auto-end active call on disconnect
            await EndCallInternal(callId, userId, "disconnected");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(userId));
        await base.OnDisconnectedAsync(exception);
    }

    // ==========================================================================
    // INITIATE CALL
    // ==========================================================================

    /// <param name="callType">"audio" or "video"</param>
    public async Task InitiateCall(Guid toUserId, string callId, string conversationId, string callType = "audio")
    {
        var fromUserId = CurrentUserId;

        // Validate friends
        var areFriends = await _friendService.AreFriendsAsync(fromUserId, toUserId);
        if (!areFriends)
        {
            await Clients.Caller.SendAsync("callError", new { callId, message = "You can only call friends" });
            return;
        }

        // Caller already in a call
        if (_userActiveCall.ContainsKey(fromUserId))
        {
            await Clients.Caller.SendAsync("callError", new { callId, message = "You are already in a call" });
            return;
        }

        // Callee is busy
        if (_userActiveCall.ContainsKey(toUserId))
        {
            await Clients.Caller.SendAsync("calleeBusy", new { callId, toUserId });
            return;
        }

        var caller = await _userRepository.GetByIdAsync(fromUserId);

        var session = new ActiveCallSession
        {
            CallId = callId,
            ConversationId = conversationId,
            CallerId = fromUserId,
            CalleeId = toUserId,
            CallType = callType,
            Status = "ringing",
            StartedAt = DateTime.UtcNow
        };

        _activeCalls[callId] = session;
        _userActiveCall[fromUserId] = callId;

        // Notify callee
        await Clients.Group(UserGroup(toUserId)).SendAsync("incomingCall", new
        {
            callId,
            conversationId,
            callerId = fromUserId,
            callerName = caller?.DisplayName ?? caller?.UserName ?? "Unknown",
            callerPhoto = caller?.ProfilePhotoUrl,
            callType
        });

        Console.WriteLine($"📞 [CallHub] Initiated: {callId} ({callType}) | {fromUserId} → {toUserId}");
    }

    // ==========================================================================
    // ANSWER / DECLINE
    // ==========================================================================

    public async Task AnswerCall(string callId)
    {
        var calleeId = CurrentUserId;

        if (!_activeCalls.TryGetValue(callId, out var session))
        {
            await Clients.Caller.SendAsync("callError",
                new { callId, message = "Call not found or already ended" });
            return;
        }

        if (session.CalleeId != calleeId)
        {
            await Clients.Caller.SendAsync("callError", new { callId, message = "Unauthorized" });
            return;
        }

        session.Status = "connecting";
        _userActiveCall[calleeId] = callId;

        // Tell caller to start WebRTC (create offer)
        await Clients.Group(UserGroup(session.CallerId)).SendAsync("callAnswered", new { callId });

        Console.WriteLine($"✅ [CallHub] Answered: {callId} ({session.CallType})");
    }

    public async Task DeclineCall(string callId)
    {
        if (!_activeCalls.TryGetValue(callId, out _)) return;
        await EndCallInternal(callId, CurrentUserId, "declined");
        Console.WriteLine($"❌ [CallHub] Declined: {callId}");
    }

    // ==========================================================================
    // WEBRTC SIGNALING
    // ==========================================================================

    public async Task SendOffer(string callId, string sdp)
    {
        var peer = GetPeer(callId, CurrentUserId);
        if (peer == null) return;
        await Clients.Group(UserGroup(peer.Value)).SendAsync("receiveOffer", new { callId, sdp });
    }

    public async Task SendAnswer(string callId, string sdp)
    {
        var peer = GetPeer(callId, CurrentUserId);
        if (peer == null) return;
        await Clients.Group(UserGroup(peer.Value)).SendAsync("receiveAnswer", new { callId, sdp });

        if (_activeCalls.TryGetValue(callId, out var session))
        {
            session.Status = "connected";
            session.ConnectedAt = DateTime.UtcNow;
        }
    }

    public async Task SendIceCandidate(string callId, string candidateJson)
    {
        var peer = GetPeer(callId, CurrentUserId);
        if (peer == null) return;
        await Clients.Group(UserGroup(peer.Value))
            .SendAsync("receiveIceCandidate", new { callId, candidate = candidateJson });
    }

    // ==========================================================================
    // CALL STATE (Mute / Video / Screen share)
    // ==========================================================================

    /// <summary>
    /// Called when local user toggles mute, video, or screen share.
    /// The peer receives peerStateChanged so it can update its UI.
    /// </summary>
    public async Task UpdateCallState(string callId, bool isMuted, bool isVideoOff, bool isScreenSharing)
    {
        var userId = CurrentUserId;
        var peer = GetPeer(callId, userId);
        if (peer == null) return;

        await Clients.Group(UserGroup(peer.Value)).SendAsync("peerStateChanged", new
        {
            callId,
            userId,
            isMuted,
            isVideoOff,
            isScreenSharing
        });
    }

    // ==========================================================================
    // END CALL
    // ==========================================================================

    public async Task EndCall(string callId)
    {
        var userId = CurrentUserId;
        await EndCallInternal(callId, userId, "ended");
        Console.WriteLine($"📵 [CallHub] Ended: {callId} by {userId}");
    }

    // ==========================================================================
    // HELPERS
    // ==========================================================================

    private static string UserGroup(Guid userId) => $"call-user:{userId}";

    /// <summary>Returns the other participant's userId, or null if session not found.</summary>
    private Guid? GetPeer(string callId, Guid userId)
    {
        if (!_activeCalls.TryGetValue(callId, out var session)) return null;
        return session.CallerId == userId ? session.CalleeId : session.CallerId;
    }

    private async Task EndCallInternal(string callId, Guid initiatedBy, string reason)
    {
        if (!_activeCalls.TryRemove(callId, out var session)) return;

        _userActiveCall.TryRemove(session.CallerId, out _);
        _userActiveCall.TryRemove(session.CalleeId, out _);

        var duration = session.ConnectedAt.HasValue
            ? (int)(DateTime.UtcNow - session.ConnectedAt.Value).TotalSeconds
            : 0;

        var payload = new { callId, reason, initiatedBy, durationSeconds = duration };

        // Notify both parties
        await Clients.Group(UserGroup(session.CallerId)).SendAsync("callEnded", payload);
        await Clients.Group(UserGroup(session.CalleeId)).SendAsync("callEnded", payload);

        // ── Persist to DB (fire-and-forget, never block the hub) ────────────
        _ = Task.Run(async () =>
        {
            try
            {
                await _callRepository.SaveCallAsync(
                    callId: Guid.Parse(callId),
                    conversationId: Guid.Parse(session.ConversationId),
                    callerId: session.CallerId,
                    calleeId: session.CalleeId,
                    callType: session.CallType,
                    startedAt: session.StartedAt,
                    connectedAt: session.ConnectedAt,
                    durationSeconds: duration,
                    reason: reason);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ [CallHub] Failed to persist call {callId}: {ex.Message}");
            }
        });
    }

    public static bool IsUserInCall(Guid userId) => _userActiveCall.ContainsKey(userId);
}

// ── Session model ────────────────────────────────────────────────────────────
public class ActiveCallSession
{
    public string CallId { get; set; } = "";
    public string ConversationId { get; set; } = "";
    public Guid CallerId { get; set; }
    public Guid CalleeId { get; set; }
    public string CallType { get; set; } = "audio";  // "audio" | "video"
    public string Status { get; set; } = "ringing";
    public DateTime StartedAt { get; set; }
    public DateTime? ConnectedAt { get; set; }
}