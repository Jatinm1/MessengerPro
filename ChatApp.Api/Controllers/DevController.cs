// ChatApp.Api/Controllers/DevController.cs
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/dev")]
public class DevController : ControllerBase
{
    [HttpGet("hash/{plain}")]
    public IActionResult Hash(string plain)
        => Ok(BCrypt.Net.BCrypt.HashPassword(plain, workFactor: 11));
}
