using ChatApp.Domain.Friends;
using ChatApp.Infrastructure.Repositories;

namespace ChatApp.Application.Services;

public class FriendService : IFriendService
{
    private readonly IFriendRepository _friends;

    public FriendService(IFriendRepository friends) => _friends = friends;

    public async Task<(bool Success, string? ErrorMessage)> SendFriendRequestAsync(Guid senderId, Guid receiverId)
    {
        var (requestId, errorMessage) = await _friends.SendFriendRequestAsync(senderId, receiverId);
        return (requestId > 0, errorMessage);
    }

    public async Task<IEnumerable<FriendRequestDto>> GetSentRequestsAsync(Guid userId)
        => await _friends.GetSentRequestsAsync(userId);

    public async Task<IEnumerable<FriendRequestDto>> GetReceivedRequestsAsync(Guid userId)
        => await _friends.GetReceivedRequestsAsync(userId);

    public async Task<(bool Success, string? ErrorMessage)> AcceptFriendRequestAsync(long requestId, Guid userId)
    {
        var error = await _friends.AcceptFriendRequestAsync(requestId, userId);
        return (error == null, error);
    }

    public async Task<(bool Success, string? ErrorMessage)> RejectFriendRequestAsync(long requestId, Guid userId)
    {
        var error = await _friends.RejectFriendRequestAsync(requestId, userId);
        return (error == null, error);
    }

    public async Task<IEnumerable<FriendDto>> GetFriendsListAsync(Guid userId)
        => await _friends.GetFriendsListAsync(userId);

    public async Task<bool> AreFriendsAsync(Guid userId, Guid friendUserId)
        => await _friends.AreFriendsAsync(userId, friendUserId);

    public async Task<IEnumerable<UserSearchResultDto>> SearchUsersAsync(Guid userId, string searchTerm)
        => await _friends.SearchUsersAsync(userId, searchTerm);
}