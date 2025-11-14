using ChatApp.Application.Services;
using ChatApp.Domain.Chat;
using ChatApp.Domain.Users;
using ChatApp.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChatApp.Api.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IUserRepository _users;

    public ChatController(IChatService chatService, IUserRepository userRepository)
    {
        _chatService = chatService;
        _users = userRepository;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                                             User.FindFirstValue("sub")!);

    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts()
    {
        var result = await _chatService.GetContactsAsync(CurrentUserId);
        return Ok(result);
    }

    [HttpGet("history/{conversationId}")]
    public async Task<IActionResult> GetChatHistory(Guid conversationId, int page = 1, int pageSize = 20)
    {
        var result = await _chatService.GetChatHistoryAsync(conversationId, CurrentUserId, page, pageSize);
        return Ok(result);
    }

    [HttpGet("all-users")]
    public async Task<IActionResult> GetAllUsers()
    {
        var result = await _users.GetAllUsersAsync();
        return Ok(result);
    }

    [HttpGet("create-conversation")]
    public async Task<IActionResult> CreateConversation(Guid userId)
    {
        var convId = await _chatService.GetOrCreateConversationAsync(CurrentUserId, userId);
        return Ok(new { conversationId = convId });
    }

    [HttpPost("mark-as-read/{conversationId}")]
    public async Task<IActionResult> MarkAsRead(Guid conversationId, [FromBody] long lastReadMessageId)
    {
        await _chatService.MarkMessagesAsReadAsync(conversationId, CurrentUserId, lastReadMessageId);
        return Ok();
    }

    // ========================================
    // GROUP ENDPOINTS
    // ========================================

    [HttpPost("group/create")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request)
    {
        var (conversationId, errorMessage) = await _chatService.CreateGroupAsync(
            CurrentUserId,
            request.GroupName,
            request.GroupPhotoUrl,
            request.MemberUserIds
        );

        if (errorMessage != null)
            return BadRequest(new { error = errorMessage });

        return Ok(new { conversationId });
    }

    [HttpGet("group/{conversationId}")]
    public async Task<IActionResult> GetGroupDetails(Guid conversationId)
    {
        var result = await _chatService.GetGroupDetailsAsync(conversationId, CurrentUserId);

        if (result == null)
            return NotFound(new { error = "Group not found or you're not a member" });

        return Ok(result);
    }

    [HttpPost("group/{conversationId}/add-member")]
    public async Task<IActionResult> AddGroupMember(Guid conversationId, [FromBody] AddGroupMemberRequest request)
    {
        var errorMessage = await _chatService.AddGroupMemberAsync(conversationId, request.UserId, CurrentUserId);

        if (errorMessage != null)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Member added successfully" });
    }

    [HttpPost("group/{conversationId}/remove-member")]
    public async Task<IActionResult> RemoveGroupMember(Guid conversationId, [FromBody] RemoveGroupMemberRequest request)
    {
        var errorMessage = await _chatService.RemoveGroupMemberAsync(conversationId, request.UserId, CurrentUserId);

        if (errorMessage != null)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Member removed successfully" });
    }

    [HttpPut("group/{conversationId}")]
    public async Task<IActionResult> UpdateGroupInfo(Guid conversationId, [FromBody] UpdateGroupInfoRequest request)
    {
        var errorMessage = await _chatService.UpdateGroupInfoAsync(
            conversationId,
            CurrentUserId,
            request.GroupName,
            request.GroupPhotoUrl
        );

        if (errorMessage != null)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Group updated successfully" });
    }

    // ========================================
    // MESSAGE STATUS ENDPOINTS
    // ========================================

    [HttpPost("message/{messageId}/status")]
    public async Task<IActionResult> UpdateMessageStatus(long messageId, [FromBody] string status)
    {
        await _chatService.UpdateMessageStatusAsync(messageId, CurrentUserId, status);
        return Ok();
    }

    [HttpGet("message/{messageId}/status")]
    public async Task<IActionResult> GetMessageStatus(long messageId)
    {
        var result = await _chatService.GetMessageStatusAsync(messageId);
        return Ok(result);
    }
}