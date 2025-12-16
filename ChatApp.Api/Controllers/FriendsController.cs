using ChatApp.Api.Hubs;
using ChatApp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

[ApiController]
[Route("api/friends")]
[Authorize]
public class FriendsController : ControllerBase
{
    private readonly IFriendService _friendService;
    private readonly IHubContext<ChatHub> _hubContext;

    public FriendsController(
        IFriendService friendService,
        IHubContext<ChatHub> hubContext)
    {
        _friendService = friendService;
        _hubContext = hubContext;
    }

    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub")!);


    /// <summary>
    /// Sends a friend request to another user.
    /// </summary>
    [HttpPost("send-request")]
    public async Task<IActionResult> SendFriendRequest(
     [FromBody] SendFriendRequestRequest request)
    {
        var senderId = CurrentUserId;

        var (success, errorMessage) =
            await _friendService.SendFriendRequestAsync(senderId, request.ReceiverId);

        if (!success)
            return BadRequest(new { error = errorMessage });

        // 🔔 REALTIME UPDATES
        var receivedRequests =
            await _friendService.GetReceivedRequestsAsync(request.ReceiverId);

        var sentRequests =
            await _friendService.GetSentRequestsAsync(senderId);

        await _hubContext.Clients.Group($"user:{request.ReceiverId}")
            .SendAsync("receivedRequestsUpdated", receivedRequests);

        await _hubContext.Clients.Group($"user:{senderId}")
            .SendAsync("sentRequestsUpdated", sentRequests);

        return Ok(new { message = "Friend request sent successfully" });
    }


    /// <summary>
    /// Retrieves all friend requests sent by the current user.
    /// </summary>
    [HttpGet("requests/sent")]
    public async Task<IActionResult> GetSentRequests()
    {
        var requests = await _friendService.GetSentRequestsAsync(CurrentUserId);
        return Ok(requests);
    }

    /// <summary>
    /// Retrieves all pending friend requests received by the current user.
    /// </summary>
    [HttpGet("requests/received")]
    public async Task<IActionResult> GetReceivedRequests()
    {
        var requests = await _friendService.GetReceivedRequestsAsync(CurrentUserId);
        return Ok(requests);
    }

    /// <summary>
    /// Accepts a received friend request by its ID.
    /// </summary>
    [HttpPost("requests/{requestId}/accept")]
    public async Task<IActionResult> AcceptRequest(long requestId)
    {
        var receiverId = CurrentUserId;

        // 1️⃣ Get request BEFORE accepting (to know sender)
        var receivedRequests =
            await _friendService.GetReceivedRequestsAsync(receiverId);

        var request =
            receivedRequests.FirstOrDefault(r => r.RequestId == requestId);

        if (request == null)
            return BadRequest(new { error = "Request not found" });

        var senderId = request.SenderId;

        // 2️⃣ Accept request
        var (success, errorMessage) =
            await _friendService.AcceptFriendRequestAsync(requestId, receiverId);

        if (!success)
            return BadRequest(new { error = errorMessage });

        // ============================
        // 🔔 REALTIME UPDATES
        // ============================

        // Receiver updates
        var updatedReceived =
            await _friendService.GetReceivedRequestsAsync(receiverId);

        var receiverFriends =
            await _friendService.GetFriendsListAsync(receiverId);

        await _hubContext.Clients.Group($"user:{receiverId}")
            .SendAsync("receivedRequestsUpdated", updatedReceived);

        await _hubContext.Clients.Group($"user:{receiverId}")
            .SendAsync("friendsListUpdated", receiverFriends);

        // Sender updates
        var senderSent =
            await _friendService.GetSentRequestsAsync(senderId);

        var senderFriends =
            await _friendService.GetFriendsListAsync(senderId);

        await _hubContext.Clients.Group($"user:{senderId}")
            .SendAsync("sentRequestsUpdated", senderSent);

        await _hubContext.Clients.Group($"user:{senderId}")
            .SendAsync("friendsListUpdated", senderFriends);

        return Ok(new { message = "Friend request accepted" });
    }



    /// <summary>
    /// Rejects a received friend request by its ID.
    /// </summary>
    [HttpPost("requests/{requestId}/reject")]
    public async Task<IActionResult> RejectRequest(long requestId)
    {
        var receiverId = CurrentUserId;

        // 1️⃣ Get request BEFORE rejecting (to know sender)
        var receivedRequests =
            await _friendService.GetReceivedRequestsAsync(receiverId);

        var request =
            receivedRequests.FirstOrDefault(r => r.RequestId == requestId);

        if (request == null)
            return BadRequest(new { error = "Request not found" });

        var senderId = request.SenderId;

        // 2️⃣ Reject request
        var (success, errorMessage) =
            await _friendService.RejectFriendRequestAsync(requestId, receiverId);

        if (!success)
            return BadRequest(new { error = errorMessage });

        // ============================
        // 🔔 REALTIME UPDATES
        // ============================

        // Receiver: update received requests
        var updatedReceived =
            await _friendService.GetReceivedRequestsAsync(receiverId);

        await _hubContext.Clients.Group($"user:{receiverId}")
            .SendAsync("receivedRequestsUpdated", updatedReceived);

        // Sender: update sent requests
        var updatedSent =
            await _friendService.GetSentRequestsAsync(senderId);

        await _hubContext.Clients.Group($"user:{senderId}")
            .SendAsync("sentRequestsUpdated", updatedSent);

        return Ok(new { message = "Friend request rejected" });
    }



    /// <summary>
    /// Retrieves the current user's list of confirmed friends.
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetFriendsList()
    {
        var friends = await _friendService.GetFriendsListAsync(CurrentUserId);
        return Ok(friends);
    }

    /// <summary>
    /// Searches for users by name, username, or email.
    /// Excludes current user and existing friends from results.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return BadRequest(new { error = "Search term is required" });

        var users = await _friendService.SearchUsersAsync(CurrentUserId, term);
        return Ok(users);
    }

    /// <summary>
    /// Checks if the current user is friends with another user.
    /// </summary>
    [HttpGet("check/{userId}")]
    public async Task<IActionResult> CheckFriendship(Guid userId)
    {
        var areFriends = await _friendService.AreFriendsAsync(CurrentUserId, userId);
        return Ok(new { areFriends });
    }
}

/// <summary>
/// Request model for sending a friend request to another user.
/// </summary>
public class SendFriendRequestRequest
{
    public Guid ReceiverId { get; set; }
}