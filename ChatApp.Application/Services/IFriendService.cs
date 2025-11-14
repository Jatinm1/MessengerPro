using ChatApp.Domain.Friends;

public interface IFriendService
{
    Task<(bool Success, string? ErrorMessage)> SendFriendRequestAsync(Guid senderId, Guid receiverId);
    Task<IEnumerable<FriendRequestDto>> GetSentRequestsAsync(Guid userId);
    Task<IEnumerable<FriendRequestDto>> GetReceivedRequestsAsync(Guid userId);
    Task<(bool Success, string? ErrorMessage)> AcceptFriendRequestAsync(long requestId, Guid userId);
    Task<(bool Success, string? ErrorMessage)> RejectFriendRequestAsync(long requestId, Guid userId);
    Task<IEnumerable<FriendDto>> GetFriendsListAsync(Guid userId);
    Task<bool> AreFriendsAsync(Guid userId, Guid friendUserId);
    Task<IEnumerable<UserSearchResultDto>> SearchUsersAsync(Guid userId, string searchTerm);
}