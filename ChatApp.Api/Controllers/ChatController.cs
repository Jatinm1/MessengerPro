using ChatApp.Application.DTOs.Chat;
using ChatApp.Application.Interfaces.IRepositories;
using ChatApp.Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChatApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IGroupService _groupService;
    private readonly IUserRepository _userRepository;

    /// <summary>
    /// Initializes a new instance of the ChatController with required services.
    /// </summary>
    public ChatController(
        IChatService chatService,
        ICloudinaryService cloudinaryService,
        IGroupService groupService,
        IUserRepository userRepository)
    {
        _chatService = chatService;
        _cloudinaryService = cloudinaryService;
        _groupService = groupService;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Gets the current user's ID from the JWT token claims.
    /// Returns either 'NameIdentifier' or 'sub' claim value as a Guid.
    /// </summary>
    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub")!);

    /// <summary>
    /// Retrieves all contacts for the current user (users they've interacted with).
    /// </summary>
    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts()
    {
        var contacts = await _chatService.GetContactsAsync(CurrentUserId);
        return Ok(contacts);
    }

    /// <summary>
    /// Retrieves all registered users in the system.
    /// </summary>
    [HttpGet("all-users")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userRepository.GetAllUsersAsync();
        return Ok(users);
    }

    /// <summary>
    /// Retrieves chat history for a specific conversation with pagination support.
    /// </summary>
    [HttpGet("history/{conversationId}")]
    public async Task<IActionResult> GetChatHistory(
        Guid conversationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var messages = await _chatService.GetChatHistoryAsync(conversationId, CurrentUserId, page, pageSize);
        return Ok(messages);
    }

    /// <summary>
    /// Gets or creates a one-on-one conversation between current user and another user.
    /// </summary>
    [HttpGet("conversation/{userId}")]
    public async Task<IActionResult> GetOrCreateConversation(Guid userId)
    {
        var conversationId = await _chatService.GetOrCreateConversationAsync(CurrentUserId, userId);
        return Ok(new { conversationId });
    }

    /// <summary>
    /// Marks all messages in a conversation as read up to a specific message ID.
    /// </summary>
    [HttpPost("mark-as-read/{conversationId}")]
    public async Task<IActionResult> MarkAsRead(
        Guid conversationId,
        [FromBody] MarkAsReadRequest request)
    {
        await _chatService.MarkMessagesAsReadAsync(
            conversationId,
            CurrentUserId,
            request.LastReadMessageId);

        return Ok();
    }

    /// <summary>
    /// Updates the delivery status of a specific message (sent, delivered, read).
    /// </summary>
    [HttpPost("message/{messageId}/status")]
    public async Task<IActionResult> UpdateMessageStatus(
        long messageId,
        [FromBody] UpdateMessageStatusRequest request)
    {
        await _chatService.UpdateMessageStatusAsync(messageId, CurrentUserId, request.Status);
        return Ok();
    }

    /// <summary>
    /// Retrieves the delivery status for all recipients of a specific message.
    /// </summary>
    [HttpGet("message/{messageId}/status")]
    public async Task<IActionResult> GetMessageStatus(long messageId)
    {
        var statuses = await _chatService.GetMessageStatusAsync(messageId);
        return Ok(statuses);
    }

    /// <summary>
    /// Deletes a message either for current user only or for all participants.
    /// </summary>
    [HttpPost("message/{messageId}/delete")]
    public async Task<IActionResult> DeleteMessage(
        long messageId,
        [FromBody] DeleteMessageRequest request)
    {
        var result = await _chatService.DeleteMessageAsync(
            messageId,
            CurrentUserId,
            request.DeleteForEveryone);

        if (!string.IsNullOrEmpty(result))
            return BadRequest(new { error = result });

        return Ok(new { message = "Message deleted successfully" });
    }

    /// <summary>
    /// Edits the content of an existing message (only if user is the sender).
    /// </summary>
    [HttpPut("message/{messageId}/edit")]
    public async Task<IActionResult> EditMessage(
        long messageId,
        [FromBody] EditMessageRequest request)
    {
        var result = await _chatService.EditMessageAsync(messageId, CurrentUserId, request.NewBody);

        if (!string.IsNullOrEmpty(result))
            return BadRequest(new { error = result });

        return Ok(new { message = "Message edited successfully" });
    }

    /// <summary>
    /// Forwards a message to another conversation.
    /// </summary>
    [HttpPost("message/{messageId}/forward")]
    public async Task<IActionResult> ForwardMessage(
        long messageId,
        [FromBody] ForwardMessageRequest request)
    {
        var (newMessageId, errorMessage) = await _chatService.ForwardMessageAsync(
            messageId,
            CurrentUserId,
            request.TargetConversationId);

        if (!string.IsNullOrEmpty(errorMessage))
            return BadRequest(new { error = errorMessage });

        return Ok(new { messageId = newMessageId, message = "Message forwarded successfully" });
    }

    /// <summary>
    /// Searches messages with optional filters.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchMessages(
        [FromQuery] string query,
        [FromQuery] Guid? senderId,
        [FromQuery] Guid? conversationId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { error = "Query parameter is required" });
        }

        var result = await _chatService.SearchMessagesAsync(
            CurrentUserId,
            query,
            senderId,
            conversationId,
            startDate,
            endDate,
            page,
            pageSize
        );

        return Ok(result);
    }

    /// <summary>
    /// Uploads a media file (image or video) to cloud storage.
    /// Maximum sizes: 10MB for images, 50MB for videos.
    /// </summary>
    [HttpPost("upload/media")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadMedia(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded" });

        var contentType = file.ContentType.ToLower();
        var isImage = contentType.StartsWith("image/");
        var isVideo = contentType.StartsWith("video/");

        if (!isImage && !isVideo)
            return BadRequest(new { error = "Only images and videos are allowed" });

        var maxSize = isImage ? 10_000_000 : 50_000_000;
        if (file.Length > maxSize)
            return BadRequest(new { error = $"File size must be less than {maxSize / 1_000_000}MB" });

        try
        {
            using var stream = file.OpenReadStream();
            var (url, publicId, error) = isImage
                ? await _cloudinaryService.UploadImageAsync(stream, file.FileName)
                : await _cloudinaryService.UploadVideoAsync(stream, file.FileName);

            if (error != null)
                return BadRequest(new { error = $"Upload failed: {error}" });

            return Ok(new
            {
                url,
                publicId,
                type = isImage ? "image" : "video",
                contentType = file.ContentType
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Upload failed: {ex.Message}" });
        }
    }
}



// DTOs for request bodies

/// <summary>
/// Request model for marking messages as read up to a specific message ID.
/// </summary>
public class MarkAsReadRequest
{
    public long LastReadMessageId { get; set; }
}

/// <summary>
/// Request model for updating a message's delivery status.
/// </summary>
public class UpdateMessageStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Request model for deleting a message (either for sender only or for all).
/// </summary>
public class DeleteMessageRequest
{
    public bool DeleteForEveryone { get; set; }
}

/// <summary>
/// Request model for editing a message's content.
/// </summary>
public class EditMessageRequest
{
    public string NewBody { get; set; } = string.Empty;
}

/// <summary>
/// Request model for forwarding a message to another conversation.
/// </summary>
public class ForwardMessageRequest
{
    public Guid TargetConversationId { get; set; }
}