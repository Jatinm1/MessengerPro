namespace ChatApp.Application.DTOs.User;



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
    bool AreFriends,
    bool ReqPending
);

public record UpdateProfileRequest(
    string? DisplayName,
    string? Bio
);

// DTOs/ContactUpdateDto.cs
//public class ContactUpdateDto
//{
//    public Guid ConversationId { get; set; }
//    public bool IsGroup { get; set; }
//    public string DisplayName { get; set; } = "";
//    public string? PhotoUrl { get; set; }
//    public string? UserId { get; set; }
//    public string? LastMessage { get; set; }
//    public DateTime? LastMessageTime { get; set; }
//    public string? LastMessageSenderId { get; set; }
//    public string? LastMessageSenderName { get; set; }
//    public int UnreadCount { get; set; }
//}

// ContactUpdateDto.cs
public class ContactUpdateDto
{
    public Guid ConversationId { get; set; }
    public bool IsGroup { get; set; }
    public string? UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string? LastMessage { get; set; }
    public string? LastMessageTime { get; set; }
    public string? LastMessageSenderId { get; set; }
    public string? LastMessageSenderName { get; set; }
    public string? LastMessageEncryptedKey { get; set; }  // ✅ NEW
    public int UnreadCount { get; set; }
}

public class EncryptedKeyDto
{
    public Guid UserId { get; set; }
    public string EncryptedKey { get; set; } = string.Empty;
}

// DTOs — ChatApp.Application/DTOs/User/

public class RegisterPublicKeyRequest
{
    public string PublicKeyJwk { get; set; } = string.Empty;
}

public class GetPublicKeysRequest
{
    public List<Guid> UserIds { get; set; } = new();
}

public class UserPublicKeyDto
{
    public string UserId { get; set; } = string.Empty;
    public string PublicKeyJwk { get; set; } = string.Empty;
}

