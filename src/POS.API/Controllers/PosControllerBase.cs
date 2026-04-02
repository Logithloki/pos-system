using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace POS.API.Controllers;

public abstract class PosControllerBase : ControllerBase
{
    protected long GetAuthenticatedUserId()
    {
        var rawClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(rawClaim, out var userId) || userId <= 0)
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        return userId;
    }
}
