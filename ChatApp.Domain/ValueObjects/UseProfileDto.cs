namespace ChatApp.Domain.ValueObjects;



public record OtherUserProfileDto(
    Guid UserId,
    string UserName,
    string DisplayName,
    string? ProfilePhotoUrl,
    string? Bio,
    DateTime CreatedAtUtc,
    DateTime? LastSeenUtc,
    bool IsOnline,
    bool AreFriends
);

