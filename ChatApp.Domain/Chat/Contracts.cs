namespace ChatApp.Domain.Chat;
public record SendDirectMessageRequest(Guid ToUserId, string Body);
public class IncomingMessageDto
{
    public long MessageId { get; set; }
    public Guid ConversationId { get; set; } // Optional — may be null in some queries
    public Guid FromUserId { get; set; }
    public string FromUserName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
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


public record UserDto(
    Guid UserId,
    string UserName,
    string DisplayName,
    DateTime CreatedAtUtc);
