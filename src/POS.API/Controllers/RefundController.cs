using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using POS.Application.Abstractions;
using POS.Application.Models;

namespace POS.API.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/refunds")]
public sealed class RefundController : ControllerBase
{
    private readonly IRefundService _refundService;

    public RefundController(IRefundService refundService)
    {
        _refundService = refundService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(RefundResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RefundResponse>> CreateRefund([FromBody] RefundRequest request, CancellationToken cancellationToken)
    {
        var authenticatedUserId = GetAuthenticatedUserId();

        var normalizedRequest = new RefundRequest
        {
            SalesOrderId = request.SalesOrderId,
            RequestedByUserId = authenticatedUserId,
            Reason = request.Reason,
        };

        var response = await _refundService.CreateRefundAsync(normalizedRequest, cancellationToken);
        return Ok(response);
    }

    private long GetAuthenticatedUserId()
    {
        var rawClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(rawClaim, out var userId) || userId <= 0)
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        return userId;
    }
}
