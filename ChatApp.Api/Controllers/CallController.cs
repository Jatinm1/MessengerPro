// ChatApp.Api/Controllers/CallController.cs
using ChatApp.Application.Interfaces.IRepositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChatApp.Api.Controllers;

[ApiController]
[Route("api/calls")]
[Authorize]
public class CallController : ControllerBase
{
    private readonly ICallRepository _callRepository;

    public CallController(ICallRepository callRepository)
    {
        _callRepository = callRepository;
    }

    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub")!);

    /// <summary>
    /// GET api/calls/history
    /// Returns the last 100 calls for the authenticated user.
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var history = await _callRepository.GetCallHistoryAsync(CurrentUserId);
        return Ok(history);
    }
}