using ChatApp.Application.Interfaces.IRepositories;
using ChatApp.Domain.Entities;

namespace ChatApp.Application.Services;

/// <summary>
/// Service responsible for friend management operations including friend requests,
/// friend list management, and user search functionality.
/// </summary>
public class FriendService : IFriendService
{
    private readonly IFriendRepository _friends;

    /// <summary>
    /// Initializes a new instance of FriendService with friend repository.
    /// </summary>
    public FriendService(IFriendRepository friends) => _friends = friends;

    /// <summary>
    /// Sends a friend request from one user to another.
    /// Returns success status and error message if any.
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> SendFriendRequestAsync(Guid senderId, Guid receiverId)
    {
        var (requestId, errorMessage) = await _friends.SendFriendRequestAsync(senderId, receiverId);
        return (requestId > 0, errorMessage);
    }

    /// <summary>
    /// Retrieves all friend requests sent by a user.
    /// </summary>
    public async Task<IEnumerable<FriendRequestDto>> GetSentRequestsAsync(Guid userId)
        => await _friends.GetSentRequestsAsync(userId);

    /// <summary>
    /// Retrieves all pending friend requests received by a user.
    /// </summary>
    public async Task<IEnumerable<FriendRequestDto>> GetReceivedRequestsAsync(Guid userId)
        => await _friends.GetReceivedRequestsAsync(userId);

    /// <summary>
    /// Accepts a received friend request.
    /// Returns success status and error message if any.
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> AcceptFriendRequestAsync(long requestId, Guid userId)
    {
        var error = await _friends.AcceptFriendRequestAsync(requestId, userId);
        return (error == null, error);
    }

    /// <summary>
    /// Rejects a received friend request.
    /// Returns success status and error message if any.
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> RejectFriendRequestAsync(long requestId, Guid userId)
    {
        var error = await _friends.RejectFriendRequestAsync(requestId, userId);
        return (error == null, error);
    }

    /// <summary>
    /// Retrieves the list of confirmed friends for a user.
    /// </summary>
    public async Task<IEnumerable<FriendDto>> GetFriendsListAsync(Guid userId)
        => await _friends.GetFriendsListAsync(userId);

    /// <summary>
    /// Checks if two users are friends.
    /// Returns true if they have an accepted friendship.
    /// </summary>
    public async Task<bool> AreFriendsAsync(Guid userId, Guid friendUserId)
        => await _friends.AreFriendsAsync(userId, friendUserId);

    /// <summary>
    /// Searches for users by name, username, or email.
    /// Excludes current user and existing friends from results.
    /// </summary>
    public async Task<IEnumerable<UserSearchResultDto>> SearchUsersAsync(Guid userId, string searchTerm)
        => await _friends.SearchUsersAsync(userId, searchTerm);
}