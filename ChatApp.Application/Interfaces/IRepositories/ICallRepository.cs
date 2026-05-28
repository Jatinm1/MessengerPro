// ChatApp.Application/Interfaces/IRepositories/ICallRepository.cs
using ChatApp.Application.DTOs.Call;

namespace ChatApp.Application.Interfaces.IRepositories;

public interface ICallRepository
{
    Task SaveCallAsync(
        Guid callId,
        Guid conversationId,
        Guid callerId,
        Guid calleeId,
        DateTime startedAt,
        DateTime? connectedAt,
        int durationSeconds,
        string reason);

    Task<IEnumerable<CallHistoryDto>> GetCallHistoryAsync(Guid userId);
}