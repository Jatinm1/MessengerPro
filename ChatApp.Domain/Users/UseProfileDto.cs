namespace ChatApp.Domain.Users;

public record UserProfileDto(
    Guid UserId,
    string UserName,
    string DisplayName,
    string? ProfilePhotoUrl,
    string? Bio,
    DateTime CreatedAtUtc,
    DateTime? LastSeenUtc,
    bool IsOnline
);

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

public record UpdateProfileRequest(
    string? DisplayName,
    string? ProfilePhotoUrl,
    string? Bio
);