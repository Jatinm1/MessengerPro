using ChatApp.Domain.Chat;
using ChatApp.Infrastructure;
using Dapper;
using System.Data;

namespace ChatApp.Infrastructure.Repositories;

public class GroupRepository : IGroupRepository
{
    private readonly DapperContext _ctx;

    public GroupRepository(DapperContext ctx) => _ctx = ctx;

    public async Task<(Guid ConversationId, string? ErrorMessage)> CreateGroupAsync(
        Guid creatorUserId,
        string groupName,
        string? groupPhotoUrl,
        List<Guid> memberUserIds)
    {
        using var con = _ctx.CreateConnection();

        var p = new DynamicParameters();
        p.Add("@CreatorUserId", creatorUserId);
        p.Add("@GroupName", groupName);
        p.Add("@GroupPhotoUrl", groupPhotoUrl);
        p.Add("@MemberUserIds", string.Join(",", memberUserIds));
        p.Add("@ConversationId", dbType: DbType.Guid, direction: ParameterDirection.Output);
        p.Add("@ErrorMessage", dbType: DbType.String, size: 500, direction: ParameterDirection.Output);

        await con.ExecuteAsync("sp_CreateGroup", p, commandType: CommandType.StoredProcedure);

        return (
            p.Get<Guid>("@ConversationId"),
            p.Get<string?>("@ErrorMessage")
        );
    }

    public async Task<GroupDetailsDto?> GetGroupDetailsAsync(Guid conversationId, Guid userId)
    {
        using var con = _ctx.CreateConnection();

        using var multi = await con.QueryMultipleAsync(
            "sp_GetGroupDetails",
            new { ConversationId = conversationId, UserId = userId },
            commandType: CommandType.StoredProcedure);

        var groupInfo = await multi.ReadFirstOrDefaultAsync<dynamic>();
        if (groupInfo == null) return null;

        var members = (await multi.ReadAsync<GroupMemberDto>()).ToList();

        return new GroupDetailsDto(
            groupInfo.ConversationId,
            groupInfo.GroupName,
            groupInfo.GroupPhotoUrl,
            groupInfo.CreatedBy,
            groupInfo.CreatorDisplayName,
            groupInfo.CreatedAtUtc,
            members
        );
    }

    public async Task<string?> AddGroupMemberAsync(Guid conversationId, Guid userId, Guid addedBy)
    {
        using var con = _ctx.CreateConnection();

        var p = new DynamicParameters();
        p.Add("@ConversationId", conversationId);
        p.Add("@UserId", userId);
        p.Add("@AddedBy", addedBy);
        p.Add("@ErrorMessage", dbType: DbType.String, direction: ParameterDirection.Output, size: 500);

        await con.ExecuteAsync("sp_AddGroupMember", p, commandType: CommandType.StoredProcedure);
        return p.Get<string?>("@ErrorMessage");
    }

    public async Task<string?> RemoveGroupMemberAsync(Guid conversationId, Guid userId, Guid removedBy)
    {
        using var con = _ctx.CreateConnection();

        var p = new DynamicParameters();
        p.Add("@ConversationId", conversationId);
        p.Add("@UserId", userId);
        p.Add("@RemovedBy", removedBy);
        p.Add("@ErrorMessage", dbType: DbType.String, direction: ParameterDirection.Output, size: 500);

        await con.ExecuteAsync("sp_RemoveGroupMember", p, commandType: CommandType.StoredProcedure);
        return p.Get<string?>("@ErrorMessage");
    }

    public async Task<string?> UpdateGroupInfoAsync(Guid conversationId, Guid userId, string? groupName, string? groupPhotoUrl)
    {
        using var con = _ctx.CreateConnection();

        var p = new DynamicParameters();
        p.Add("@ConversationId", conversationId);
        p.Add("@UserId", userId);
        p.Add("@GroupName", groupName);
        p.Add("@GroupPhotoUrl", groupPhotoUrl);
        p.Add("@ErrorMessage", dbType: DbType.String, direction: ParameterDirection.Output, size: 500);

        await con.ExecuteAsync("sp_UpdateGroupInfo", p, commandType: CommandType.StoredProcedure);
        return p.Get<string?>("@ErrorMessage");
    }

    public async Task<string?> UpdateGroupPhotoAsync(Guid conversationId, Guid userId, string groupPhotoUrl)
    {
        using var con = _ctx.CreateConnection();

        var p = new DynamicParameters();
        p.Add("@ConversationId", conversationId);
        p.Add("@UserId", userId);
        p.Add("@GroupPhotoUrl", groupPhotoUrl);
        p.Add("@ErrorMessage", dbType: DbType.String, direction: ParameterDirection.Output, size: 500);

        await con.ExecuteAsync("sp_UpdateGroupPhoto", p, commandType: CommandType.StoredProcedure);
        return p.Get<string?>("@ErrorMessage");
    }

    public async Task<string?> DeleteGroupAsync(Guid conversationId, Guid userId)
    {
        using var con = _ctx.CreateConnection();

        var p = new DynamicParameters();
        p.Add("@ConversationId", conversationId);
        p.Add("@UserId", userId);
        p.Add("@ErrorMessage", dbType: DbType.String, direction: ParameterDirection.Output, size: 500);

        await con.ExecuteAsync("sp_DeleteGroup", p, commandType: CommandType.StoredProcedure);
        return p.Get<string?>("@ErrorMessage");
    }
}