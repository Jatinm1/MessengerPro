namespace ChatApp.Application.DTOs.Chat;

public record SendDirectMessageRequest(
    Guid ToUserId,
    string Body,
    string? ContentType = "text",
    string? MediaUrl = null);

public record SendGroupMessageRequest(
    Guid ConversationId,
    string Body,
    string? ContentType = "text",
    string? MediaUrl = null);

public record MessageSentDto(
    long MessageId,
    Guid ConversationId,
    Guid FromUserId,
    string FromUserName,
    string FromDisplayName,
    string Body,
    string ContentType,
    string? MediaUrl,
    DateTime CreatedAtUtc,
    string MessageStatus,
    List<Guid> RecipientIds
);

//public record ContactDto(
//    Guid ConversationId,
//    bool IsGroup,
//    Guid? UserId,
//    string UserName,
//    string DisplayName,
//    string? PhotoUrl,
//    bool IsOnline,
//    DateTime? LastSeenUtc,
//    DateTime? LastMessageTime,
//    string? LastMessage,
//    int UnreadCount
//);

public class ContactDto
{
    public Guid ConversationId { get; set; }
    public bool IsGroup { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public bool IsOnline { get; set; }
    public string? LastSeenUtc { get; set; }
    public string? LastMessageTime { get; set; }
    public string? LastMessage { get; set; }
    public string? LastMessageEncryptedKey { get; set; }  // ✅ NEW
    public int UnreadCount { get; set; }
}



// MessageWithStatusDto.cs
public class MessageWithStatusDto
{
    public long MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid FromUserId { get; set; }
    public string FromUserName { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? EditedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public bool DeletedForEveryone { get; set; }
    public string? MessageStatus { get; set; }
    public string? EncryptedKey { get; set; }  // ← add this
}

public record MessageStatusDto(
    long MessageId,
    Guid UserId,
    string DisplayName,
    string Status,
    DateTime StatusTimestamp
);

public record DeleteMessageRequest(
    bool DeleteForEveryone
);

public record EditMessageRequest(
    string NewBody
);

public record ForwardMessageRequest(
    Guid TargetConversationId
);

public record SearchResultDto(
    long MessageId,
    Guid ConversationId,
    Guid SenderId,
    string SenderDisplayName,
    string? SenderPhotoUrl,
    string Body,
    string ContentType,
    string? MediaUrl,
    DateTime CreatedAtUtc,
    bool IsGroup,
    string? GroupName,
    string ConversationName,
    string MatchedText
);

public record SearchResponseDto(
    IEnumerable<SearchResultDto> Results,
    int TotalCount,
    int Page,
    int PageSize
);