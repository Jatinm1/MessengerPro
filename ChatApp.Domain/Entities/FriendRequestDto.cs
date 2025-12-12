using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatApp.Domain.Entities
{
    public record FriendRequestDto(
        long RequestId,
        Guid SenderId,
        string SenderUserName,
        string SenderDisplayName,
        Guid ReceiverId,
        string ReceiverUserName,
        string ReceiverDisplayName,
        string Status,
        DateTime CreatedAtUtc,
        DateTime? UpdatedAtUtc
    );

    public record FriendDto(
        Guid FriendUserId,
        string FriendUserName,
        string FriendDisplayName,
        DateTime FriendsSince
    );

    public record UserSearchResultDto(
        Guid UserId,
        string UserName,
        string DisplayName,
        DateTime CreatedAtUtc,
        string RelationshipStatus
    );

    public record SendFriendRequestRequest(Guid ReceiverId);
    public record RespondToRequestRequest(long RequestId, bool Accept);
}
