using ChatApp.Application.Services;
using ChatApp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChatApp.Api.Controllers;

[ApiController]
[Route("api/friends")]
[Authorize]
public class FriendsController : ControllerBase
{
    private readonly IFriendService _friendService;

    /// <summary>
    /// Initializes a new instance of the FriendsController with friend management service.
    /// </summary>
    public FriendsController(IFriendService friendService)
    {
        _friendService = friendService;
    }

    /// <summary>
    /// Gets the current user's ID from the JWT token claims.
    /// Returns either 'NameIdentifier' or 'sub' claim value as a Guid.
    /// </summary>
    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub")!);

    /// <summary>
    /// Sends a friend request to another user.
    /// </summary>
    [HttpPost("send-request")]
    public async Task<IActionResult> SendFriendRequest([FromBody] SendFriendRequestRequest request)
    {
        var (success, errorMessage) = await _friendService.SendFriendRequestAsync(CurrentUserId, request.ReceiverId);

        if (!success)
            return BadRequest(new { error = errorMessage });

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
        var (success, errorMessage) = await _friendService.AcceptFriendRequestAsync(requestId, CurrentUserId);

        if (!success)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Friend request accepted" });
    }

    /// <summary>
    /// Rejects a received friend request by its ID.
    /// </summary>
    [HttpPost("requests/{requestId}/reject")]
    public async Task<IActionResult> RejectRequest(long requestId)
    {
        var (success, errorMessage) = await _friendService.RejectFriendRequestAsync(requestId, CurrentUserId);

        if (!success)
            return BadRequest(new { error = errorMessage });

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