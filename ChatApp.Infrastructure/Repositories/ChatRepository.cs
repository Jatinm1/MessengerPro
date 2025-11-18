using ChatApp.Domain.Chat;
using Dapper;
using System.Data;

namespace ChatApp.Infrastructure.Repositories;

public class ChatRepository : IChatRepository
{
    private readonly DapperContext _ctx;
    public ChatRepository(DapperContext ctx) => _ctx = ctx;

    // ----------------------------
    // Conversation Handling
    // ----------------------------
    public async Task<Guid> GetOrCreateDirectConversationAsync(Guid a, Guid b)
    {
        using var con = _ctx.CreateConnection();
        var p = new DynamicParameters();
        p.Add("@UserA", a);
        p.Add("@UserB", b);
        p.Add("@ConversationId", dbType: DbType.Guid, direction: ParameterDirection.Output);
        await con.ExecuteAsync("sp_GetOrCreateDirectConversation", p, commandType: CommandType.StoredProcedure);
        return p.Get<Guid>("@ConversationId");
    }

    // ----------------------------
    // Message Saving (Overloaded)
    // ----------------------------

    // TEXT ONLY
    public async Task<long> SaveMessageAsync(Guid conversationId, Guid senderId, string body, string contentType)
    {
        using var con = _ctx.CreateConnection();
        return await con.ExecuteScalarAsync<long>(
            "sp_SaveMessage",
            new
            {
                ConversationId = conversationId,
                SenderId = senderId,
                Body = body,
                ContentType = contentType,
                MediaUrl = (string?)null
            },
            commandType: CommandType.StoredProcedure
        );
    }

    // TEXT + IMAGE/VIDEO
    public async Task<long> SaveMessageAsync(Guid conversationId, Guid senderId, string body, string contentType, string? mediaUrl)
    {
        using var con = _ctx.CreateConnection();
        return await con.ExecuteScalarAsync<long>(
            "sp_SaveMessage",
            new
            {
                ConversationId = conversationId,
                SenderId = senderId,
                Body = body,
                ContentType = contentType,
                MediaUrl = mediaUrl
            },
            commandType: CommandType.StoredProcedure
        );
    }

    // ----------------------------
    // Contacts
    // ----------------------------
    public async Task<IEnumerable<ContactDto>> GetContactsAsync(Guid userId)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryAsync<ContactDto>(
            "sp_GetContactsByUserId",
            new { UserId = userId },
            commandType: CommandType.StoredProcedure);
    }

    // ----------------------------
    // Chat History
    // ----------------------------
    public async Task<IEnumerable<MessageWithStatusDto>> GetMessagesWithStatusAsync(Guid conversationId, Guid userId, int page, int pageSize)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryAsync<MessageWithStatusDto>(
            "sp_GetMessagesWithStatus",
            new { ConversationId = conversationId, UserId = userId, Page = page, PageSize = pageSize },
            commandType: CommandType.StoredProcedure);
    }

    // ----------------------------
    // Mark Messages Read + return affected senders
    // ----------------------------
    public async Task<IEnumerable<Guid>> MarkMessagesAsReadAsync(Guid conversationId, Guid userId, long lastReadMessageId)
    {
        using var con = _ctx.CreateConnection();

        return await con.QueryAsync<Guid>(
            "sp_MarkMessagesAsRead",
            new
            {
                ConversationId = conversationId,
                UserId = userId,
                LastReadMessageId = lastReadMessageId
            },
            commandType: CommandType.StoredProcedure);
    }

    // ----------------------------
    // Group Management
    // ----------------------------
    public async Task<(Guid ConversationId, string? ErrorMessage)> CreateGroupAsync(Guid creatorUserId, string groupName, string? groupPhotoUrl, List<Guid> memberUserIds)
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

    // ----------------------------
    // Message Status
    // ----------------------------
    public async Task UpdateMessageStatusAsync(long messageId, Guid userId, string status)
    {
        using var con = _ctx.CreateConnection();
        await con.ExecuteAsync(
            "sp_UpdateMessageStatus",
            new { MessageId = messageId, UserId = userId, Status = status },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<Guid?> GetSenderIdByMessageIdAsync(long messageId)
    {
        using var con = _ctx.CreateConnection();
        return await con.ExecuteScalarAsync<Guid?>(
            "SELECT SenderId FROM Messages WHERE MessageId = @MessageId",
            new { MessageId = messageId });
    }

    public async Task<IEnumerable<MessageStatusDto>> GetMessageStatusAsync(long messageId)
    {
        using var con = _ctx.CreateConnection();
        return await con.QueryAsync<MessageStatusDto>(
            "sp_GetMessageStatus",
            new { MessageId = messageId },
            commandType: CommandType.StoredProcedure);
    }
}
