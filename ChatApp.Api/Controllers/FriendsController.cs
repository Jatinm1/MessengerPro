using ChatApp.Application.Services;
using ChatApp.Domain.Friends;
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

    public FriendsController(IFriendService friendService)
    {
        _friendService = friendService;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                                             User.FindFirstValue("sub")!);

    // POST: api/friends/send-request
    [HttpPost("send-request")]
    public async Task<IActionResult> SendFriendRequest([FromBody] SendFriendRequestRequest request)
    {
        var (success, errorMessage) = await _friendService.SendFriendRequestAsync(CurrentUserId, request.ReceiverId);

        if (!success)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Friend request sent successfully" });
    }

    // GET: api/friends/requests/sent
    [HttpGet("requests/sent")]
    public async Task<IActionResult> GetSentRequests()
    {
        var requests = await _friendService.GetSentRequestsAsync(CurrentUserId);
        return Ok(requests);
    }

    // GET: api/friends/requests/received
    [HttpGet("requests/received")]
    public async Task<IActionResult> GetReceivedRequests()
    {
        var requests = await _friendService.GetReceivedRequestsAsync(CurrentUserId);
        return Ok(requests);
    }

    // POST: api/friends/requests/{requestId}/accept
    [HttpPost("requests/{requestId}/accept")]
    public async Task<IActionResult> AcceptRequest(long requestId)
    {
        var (success, errorMessage) = await _friendService.AcceptFriendRequestAsync(requestId, CurrentUserId);

        if (!success)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Friend request accepted" });
    }

    // POST: api/friends/requests/{requestId}/reject
    [HttpPost("requests/{requestId}/reject")]
    public async Task<IActionResult> RejectRequest(long requestId)
    {
        var (success, errorMessage) = await _friendService.RejectFriendRequestAsync(requestId, CurrentUserId);

        if (!success)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Friend request rejected" });
    }

    // GET: api/friends/list
    [HttpGet("list")]
    public async Task<IActionResult> GetFriendsList()
    {
        var friends = await _friendService.GetFriendsListAsync(CurrentUserId);
        return Ok(friends);
    }

    // GET: api/friends/search?term=john
    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return BadRequest(new { error = "Search term is required" });

        var users = await _friendService.SearchUsersAsync(CurrentUserId, term);
        return Ok(users);
    }

    // GET: api/friends/check/{userId}
    [HttpGet("check/{userId}")]
    public async Task<IActionResult> CheckFriendship(Guid userId)
    {
        var areFriends = await _friendService.AreFriendsAsync(CurrentUserId, userId);
        return Ok(new { areFriends });
    }
}