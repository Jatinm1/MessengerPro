using ChatApp.Api.SignalR;
using ChatApp.Application.Services;
using ChatApp.Domain.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ChatApp.Api.Controllers;

[ApiController]
[Route("api/group")]
[Authorize]
public class GroupController : ControllerBase
{
    private readonly IGroupService _groupService;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IHubContext<ChatHub> _hub;


    public GroupController(IGroupService groupService, ICloudinaryService cloudinaryService,IHubContext<ChatHub> hub)
    {
        _groupService = groupService;
        _cloudinaryService = cloudinaryService;
        _hub = hub;
    }

    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub")!
    );

    [HttpPost("create")]
    public async Task<IActionResult> CreateGroup(CreateGroupRequest request)
    {
        var creatorId = CurrentUserId;

        var (conversationId, errorMessage) = await _groupService.CreateGroupAsync(
            creatorId,
            request.GroupName,
            request.GroupPhotoUrl,
            request.MemberUserIds
        );

        if (errorMessage != null)
            return BadRequest(new { error = errorMessage });

        // Get full group details
        var groupDetails = await _groupService.GetGroupDetailsAsync(conversationId.Value, CurrentUserId);

        // 🔥 Notify ALL members
        foreach (var member in groupDetails.Members)
        {
            await _hub.Clients.Group($"user:{member.UserId}")
                .SendAsync("groupCreated", groupDetails);
        }

        return Ok(groupDetails);
    }



    // GET: api/group/{conversationId}
    [HttpGet("{conversationId}")]
    public async Task<IActionResult> GetGroupDetails(Guid conversationId)
    {
        var result = await _groupService.GetGroupDetailsAsync(conversationId, CurrentUserId);

        if (result == null)
            return NotFound(new { error = "Group not found or you're not a member" });

        return Ok(result);
    }

    // POST: api/group/{conversationId}/add-member
    [HttpPost("{conversationId}/add-member")]
    public async Task<IActionResult> AddGroupMember(Guid conversationId, [FromBody] AddGroupMemberRequest request)
    {
        var errorMessage = await _groupService.AddGroupMemberAsync(conversationId, request.UserId, CurrentUserId);

        if (errorMessage != null)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Member added successfully" });
    }

    // POST: api/group/{conversationId}/remove-member
    [HttpPost("{conversationId}/remove-member")]
    public async Task<IActionResult> RemoveGroupMember(Guid conversationId, [FromBody] RemoveGroupMemberRequest request)
    {
        var errorMessage = await _groupService.RemoveGroupMemberAsync(conversationId, request.UserId, CurrentUserId);

        if (errorMessage != null)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Member removed successfully" });
    }

    // PUT: api/group/{conversationId} (WITHOUT photo)
    [HttpPut("{conversationId}")]
    public async Task<IActionResult> UpdateGroupInfo(Guid conversationId, [FromBody] UpdateGroupInfoRequest request)
    {
        var errorMessage = await _groupService.UpdateGroupInfoAsync(
            conversationId,
            CurrentUserId,
            request.GroupName,
            null // Don't update photo via this endpoint
        );

        if (errorMessage != null)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Group updated successfully" });
    }

    [HttpDelete("{conversationId}")]
    public async Task<IActionResult> DeleteGroup(Guid conversationId)
    {
        var userId = CurrentUserId;

        // Get details BEFORE deletion
        var groupDetails = await _groupService.GetGroupDetailsAsync(conversationId, userId);
        if (groupDetails == null)
            return NotFound();

        // Delete it
        var error = await _groupService.DeleteGroupAsync(conversationId, userId);
        if (error != null)
            return BadRequest(new { error });

        // 🔥 Broadcast deletion notification
        foreach (var member in groupDetails.Members)
        {
            await _hub.Clients.Group($"user:{member.UserId}")
                .SendAsync("groupDeleted", new
                {
                    conversationId,
                    groupName = groupDetails.GroupName,
                    deletedBy = userId
                });
        }

        return Ok(new { message = "Group deleted successfully" });
    }

    // POST: api/group/{conversationId}/photo
    [HttpPost("{conversationId}/photo")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadGroupPhoto(Guid conversationId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded" });

        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };

        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(new { error = "Invalid file type. Only images are allowed." });

        try
        {
            using var stream = file.OpenReadStream();
            var (url, publicId, error) = await _cloudinaryService.UploadImageAsync(stream, file.FileName);

            if (error != null)
                return BadRequest(new { error = $"Upload failed: {error}" });

            // Update group photo
            var errorMessage = await _groupService.UpdateGroupPhotoAsync(conversationId, CurrentUserId, url);

            if (errorMessage != null)
                return BadRequest(new { error = errorMessage });

            return Ok(new { url, publicId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Upload failed: {ex.Message}" });
        }
    }
}