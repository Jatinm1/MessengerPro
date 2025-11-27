namespace ChatApp.Domain.Chat;

public enum MessageStatusEnum
{
    Sent,
    Delivered,
    Read
}

public record MessageStatusDto(
    long MessageId,
    Guid UserId,
    string DisplayName,
    string Status,
    DateTime StatusTimestamp
);

// Update existing MessageWithStatusDto in ChatApp.Domain/Chat/Models.cs


public record MessageWithStatusDto(
    long MessageId,
    Guid ConversationId,
    Guid FromUserId,
    string FromUserName,
    string FromDisplayName,
    string? Body,  // Can be null if deleted
    string ContentType,
    string? MediaUrl,
    DateTime CreatedAtUtc,
    bool IsEdited,
    DateTime? EditedAtUtc,
    bool IsDeleted,
    bool DeletedForEveryone,
    string? MessageStatus
);
