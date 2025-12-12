using ChatApp.Api.Hubs;
using ChatApp.Application.DTOs.Group;
using ChatApp.Application.Interfaces.IServices;
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
    private readonly IChatService _chatService;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IHubContext<ChatHub> _hub;

    /// <summary>
    /// Initializes a new instance of the GroupController with group management services.
    /// </summary>
    public GroupController(
        IGroupService groupService,
        ICloudinaryService cloudinaryService,
        IHubContext<ChatHub> hub,
        IChatService chatService)
    {
        _groupService = groupService;
        _cloudinaryService = cloudinaryService;
        _hub = hub;
        _chatService = chatService;
    }

    /// <summary>
    /// Gets the current user's ID from the JWT token claims.
    /// Returns either 'NameIdentifier' or 'sub' claim value as a Guid.
    /// </summary>
    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub")!
    );

    /// <summary>
    /// Creates a new group chat with specified members and notifies all participants.
    /// </summary>
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

        // Notify all group members via SignalR
        foreach (var member in groupDetails.Members)
        {
            await _hub.Clients.Group($"user:{member.UserId}")
                .SendAsync("groupCreated", groupDetails);
        }

        return Ok(groupDetails);
    }

    /// <summary>
    /// Retrieves detailed information about a specific group.
    /// </summary>
    [HttpGet("{conversationId}")]
    public async Task<IActionResult> GetGroupDetails(Guid conversationId)
    {
        var result = await _groupService.GetGroupDetailsAsync(conversationId, CurrentUserId);

        if (result == null)
            return NotFound(new { error = "Group not found or you're not a member" });

        return Ok(result);
    }

    /// <summary>
    /// Checks if the current user is an administrator of the specified group.
    /// </summary>
    [HttpGet("{conversationId}/is-admin")]
    public async Task<IActionResult> IsUserAdmin(Guid conversationId)
    {
        var isAdmin = await _chatService.IsUserAdminAsync(conversationId, CurrentUserId);
        return Ok(new { isAdmin });
    }

    /// <summary>
    /// Adds a new member to an existing group.
    /// </summary>
    [HttpPost("{conversationId}/add-member")]
    public async Task<IActionResult> AddGroupMember(Guid conversationId, [FromBody] AddGroupMemberRequest request)
    {
        var errorMessage = await _groupService.AddGroupMemberAsync(conversationId, request.UserId, CurrentUserId);

        if (errorMessage != null)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Member added successfully" });
    }

    /// <summary>
    /// Removes a member from a group (admin-only action).
    /// </summary>
    [HttpPost("{conversationId}/remove-member")]
    public async Task<IActionResult> RemoveGroupMember(Guid conversationId, [FromBody] RemoveGroupMemberRequest request)
    {
        var errorMessage = await _groupService.RemoveGroupMemberAsync(conversationId, request.UserId, CurrentUserId);

        if (errorMessage != null)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Member removed successfully" });
    }

    /// <summary>
    /// Allows the current user to leave a group.
    /// </summary>
    [HttpPost("{conversationId}/leave")]
    public async Task<IActionResult> LeaveGroup(Guid conversationId)
    {
        var errorMessage = await _chatService.LeaveGroupAsync(conversationId, CurrentUserId);

        if (errorMessage != null)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Left group successfully" });
    }

    /// <summary>
    /// Updates basic group information (name, description).
    /// </summary>
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

    /// <summary>
    /// Transfers group administration to another member.
    /// </summary>
    [HttpPost("{conversationId}/transfer-admin")]
    public async Task<IActionResult> TransferAdmin(Guid conversationId, [FromBody] TransferAdminRequest request)
    {
        var errorMessage = await _chatService.TransferAdminAsync(
            conversationId,
            CurrentUserId,
            request.NewAdminId);

        if (errorMessage != null)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Admin transferred successfully" });
    }

    /// <summary>
    /// Permanently deletes a group and notifies all members.
    /// </summary>
    [HttpDelete("{conversationId}")]
    public async Task<IActionResult> DeleteGroup(Guid conversationId)
    {
        var userId = CurrentUserId;

        // Get details BEFORE deletion
        var groupDetails = await _groupService.GetGroupDetailsAsync(conversationId, userId);
        if (groupDetails == null)
            return NotFound();

        // Delete group
        var error = await _groupService.DeleteGroupAsync(conversationId, userId);
        if (error != null)
            return BadRequest(new { error });

        // Notify all members about deletion
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

    /// <summary>
    /// Uploads and sets a new group profile photo.
    /// Maximum file size: 10MB. Allowed types: JPEG, PNG, GIF, WebP.
    /// </summary>
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

            // Update group photo URL in database
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

// DTOs for GroupController

/// <summary>
/// Request model for transferring group administration to another member.
/// </summary>
public class TransferAdminRequest
{
    public Guid NewAdminId { get; set; }
}