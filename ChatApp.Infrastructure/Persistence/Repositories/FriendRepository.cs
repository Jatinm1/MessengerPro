using ChatApp.Application.Interfaces.IRepositories;
using ChatApp.Domain.Entities;
using ChatApp.Infrastructure.Persistence;
using Dapper;
using System.Data;

namespace ChatApp.Infrastructure.Persistence.Repositories;

public class FriendRepository : IFriendRepository
{
    private readonly DapperContext _ctx;

    public FriendRepository(DapperContext ctx) => _ctx = ctx;

    public async Task<(long RequestId, string? ErrorMessage)> SendFriendRequestAsync(Guid senderId, Guid receiverId)
    {
        using var con = _ctx.CreateConnection();
        var p = new DynamicParameters();
        p.Add("@SenderId", senderId);
        p.Add("@ReceiverId", receiverId);
        p.Add("@RequestId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        p.Add("@ErrorMessage", dbType: DbType.String, size: 500, direction: ParameterDirection.Output);

        await con.ExecuteAsync("sp_SendFriendRequest", p, commandType: CommandType.StoredProcedure);

        var requestId = p.Get<long?>("@RequestId") ?? 0;
        var errorMessage = p.Get<string?>("@ErrorMessage");

        return (requestId, errorMessage);
    }

    public async Task<IEnumerable<FriendRequestDto>> GetSentRequestsAsync(Guid userId)
    {
        using var con = _ctx.CreateConnection();
        var results = await con.QueryAsync<dynamic>(
            "sp_GetSentFriendRequests",
            new { UserId = userId },
            commandType: CommandType.StoredProcedure);

        return results.Select(r => new FriendRequestDto(
            r.RequestId,
            r.SenderId,
            "",
            "",
            r.ReceiverId,
            r.ReceiverUserName,
            r.ReceiverDisplayName,
            r.Status,
            r.CreatedAtUtc,
            r.UpdatedAtUtc
        ));
    }

    public async Task<IEnumerable<FriendRequestDto>> GetReceivedRequestsAsync(Guid userId)
    {
        using var con = _ctx.CreateConnection();
        var results = await con.QueryAsync<dynamic>(
            "sp_GetReceivedFriendRequests",
            new { UserId = userId },
            commandType: CommandType.StoredProcedure);

        return results.Select(r => new FriendRequestDto(
            r.RequestId,
            r.SenderId,
            r.SenderUserName,
            r.SenderDisplayName,
            r.ReceiverId,
            "",
            "",
            r.Status,
            r.CreatedAtUtc,
            r.UpdatedAtUtc
        ));
    }

    public async Task<string?> AcceptFriendRequestAsync(long requestId, Guid userId)
    {
        using var con = _ctx.CreateConnection();
        var p = new DynamicParameters();
        p.Add("@RequestId", requestId);
        p.Add("@UserId", userId);
        p.Add("@ErrorMessage", dbType: DbType.String, size: 500, direction: ParameterDirection.Output);

        await con.ExecuteAsync("sp_AcceptFriendRequest", p, commandType: CommandType.StoredProcedure);
        return p.Get<string?>("@ErrorMessage");
    }

    public async Task<string?> RejectFriendRequestAsync(long requestId, Guid userId)
    {
        using var con = _ctx.CreateConnection();
        var p = new DynamicParameters();
        p.Add("@RequestId", requestId);
        p.Add("@UserId", userId);
        p.Add("@ErrorMessage", dbType: DbType.String, size: 500, direction: ParameterDirection.Output);

        await con.ExecuteAsync("sp_RejectFriendRequest", p, commandType: CommandType.StoredProcedure);
        return p.Get<string?>("@ErrorMessage");
    }

    public async Task<IEnumerable<FriendDto>> GetFriendsListAsync(Guid userId)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryAsync<FriendDto>(
            "sp_GetFriendsList",
            new { UserId = userId },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<bool> AreFriendsAsync(Guid userId, Guid friendUserId)
    {
        using var con = _ctx.CreateConnection();
        var p = new DynamicParameters();
        p.Add("@UserId", userId);
        p.Add("@FriendUserId", friendUserId);
        p.Add("@AreFriends", dbType: DbType.Boolean, direction: ParameterDirection.Output);

        await con.ExecuteAsync("sp_CheckFriendship", p, commandType: CommandType.StoredProcedure);
        return p.Get<bool>("@AreFriends");
    }

    public async Task<IEnumerable<UserSearchResultDto>> SearchUsersAsync(Guid userId, string searchTerm)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryAsync<UserSearchResultDto>(
            "sp_SearchUsers",
            new { UserId = userId, SearchTerm = searchTerm },
            commandType: CommandType.StoredProcedure);
    }
}