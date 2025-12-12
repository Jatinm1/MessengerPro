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

public record ContactDto(
    Guid ConversationId,
    bool IsGroup,
    Guid? UserId,
    string UserName,
    string DisplayName,
    string? PhotoUrl,
    bool IsOnline,
    DateTime? LastSeenUtc,
    DateTime? LastMessageTime,
    string? LastMessage,
    int UnreadCount
);

public record MessageWithStatusDto(
    long MessageId,
    Guid ConversationId,
    Guid FromUserId,
    string FromUserName,
    string FromDisplayName,
    string? Body,
    string ContentType,
    string? MediaUrl,
    DateTime CreatedAtUtc,
    bool IsEdited,
    DateTime? EditedAtUtc,
    bool IsDeleted,
    bool DeletedForEveryone,
    string? MessageStatus
);

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