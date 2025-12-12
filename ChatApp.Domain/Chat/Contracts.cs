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



