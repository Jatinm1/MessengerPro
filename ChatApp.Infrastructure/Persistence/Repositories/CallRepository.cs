// ============================================================
// ChatApp.Infrastructure/Persistence/Repositories/CallRepository.cs
// ============================================================
using ChatApp.Application.DTOs.Call;
using ChatApp.Application.Interfaces.IRepositories;
using ChatApp.Infrastructure.Persistence;
using Dapper;
using System.Data;

namespace ChatApp.Infrastructure.Persistence.Repositories;

public class CallRepository : ICallRepository
{
    private readonly DapperContext _ctx;
    public CallRepository(DapperContext ctx) => _ctx = ctx;

    public async Task SaveCallAsync(
        Guid callId,
        Guid conversationId,
        Guid callerId,
        Guid calleeId,
        string callType,
        DateTime startedAt,
        DateTime? connectedAt,
        int durationSeconds,
        string reason)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            "sp_SaveCallHistory",
            new
            {
                CallId = callId,
                ConversationId = conversationId,
                CallerId = callerId,
                CalleeId = calleeId,
                CallType = callType,
                StartedAt = startedAt,
                ConnectedAt = connectedAt,
                EndedAt = DateTime.UtcNow,
                DurationSeconds = durationSeconds,
                Reason = reason
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<CallHistoryDto>> GetCallHistoryAsync(Guid userId)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryAsync<CallHistoryDto>(
            "sp_GetCallHistoryForUser",
            new { UserId = userId },
            commandType: CommandType.StoredProcedure);
    }
}
