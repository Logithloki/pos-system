using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        var response = await _refundService.CreateRefundAsync(request, cancellationToken);
        return Ok(response);
    }
}
