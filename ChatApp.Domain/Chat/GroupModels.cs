namespace ChatApp.Domain.Chat;

public record CreateGroupRequest(
    string GroupName,
    string? GroupPhotoUrl,
    List<Guid> MemberUserIds
);

public record GroupDetailsDto(
    Guid ConversationId,
    string GroupName,
    string? GroupPhotoUrl,
    Guid? CreatedBy,
    string? CreatorDisplayName,
    DateTime CreatedAtUtc,
    List<GroupMemberDto> Members
);

public record GroupMemberDto(
    Guid UserId,
    string UserName,
    string DisplayName,
    string? ProfilePhotoUrl,
    DateTime JoinedAtUtc,
    bool IsAdmin
);

public record AddGroupMemberRequest(
    Guid UserId
);

public record RemoveGroupMemberRequest(
    Guid UserId
);

public record UpdateGroupInfoRequest(
    string? GroupName,
    string? GroupPhotoUrl
);