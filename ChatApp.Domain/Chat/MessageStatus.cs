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

public record MessageWithStatusDto(
    long MessageId,
    Guid ConversationId,
    Guid FromUserId,
    string FromUserName,
    string FromDisplayName,
    string Body,
    DateTime CreatedAtUtc,
    string? MessageStatus // For sender: overall status (Sent/Delivered/Read)
);