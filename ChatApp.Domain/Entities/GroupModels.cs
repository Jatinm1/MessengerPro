namespace ChatApp.Domain.Entities;



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