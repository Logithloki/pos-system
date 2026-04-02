using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace POS.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/system")]
public sealed class SystemController : ControllerBase
{
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(
            new
            {
                status = "ok",
                utc = DateTime.UtcNow,
                offlineFirst = true,
            });
    }
}
