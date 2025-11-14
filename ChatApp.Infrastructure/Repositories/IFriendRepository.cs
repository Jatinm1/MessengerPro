using ChatApp.Domain.Friends;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatApp.Infrastructure.Repositories
{
    public interface IFriendRepository
    {
        Task<(long RequestId, string? ErrorMessage)> SendFriendRequestAsync(Guid senderId, Guid receiverId);
        Task<IEnumerable<FriendRequestDto>> GetSentRequestsAsync(Guid userId);
        Task<IEnumerable<FriendRequestDto>> GetReceivedRequestsAsync(Guid userId);
        Task<string?> AcceptFriendRequestAsync(long requestId, Guid userId);
        Task<string?> RejectFriendRequestAsync(long requestId, Guid userId);
        Task<IEnumerable<FriendDto>> GetFriendsListAsync(Guid userId);
        Task<bool> AreFriendsAsync(Guid userId, Guid friendUserId);
        Task<IEnumerable<UserSearchResultDto>> SearchUsersAsync(Guid userId, string searchTerm);
    }
}
