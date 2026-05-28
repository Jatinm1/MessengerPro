// ChatApp.Application/DTOs/Call/CallHistoryDto.cs
namespace ChatApp.Application.DTOs.Call;

public class CallHistoryDto
{
    public Guid CallId { get; init; }
    public Guid ConversationId { get; init; }

    public Guid CallerId { get; init; }
    public string CallerDisplayName { get; init; } = "";
    public string CallerUserName { get; init; } = "";
    public string? CallerPhotoUrl { get; init; }

    public Guid CalleeId { get; init; }
    public string CalleeDisplayName { get; init; } = "";
    public string CalleeUserName { get; init; } = "";
    public string? CalleePhotoUrl { get; init; }

    public DateTime StartedAt { get; init; }
    public DateTime? ConnectedAt { get; init; }
    public DateTime? EndedAt { get; init; }
    public int DurationSeconds { get; init; }
    public string Reason { get; init; } = "ended";
}