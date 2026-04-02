using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Abstractions;

namespace POS.API.Controllers;

[ApiController]
[Authorize(Policy = "CashierOrAdmin")]
[Route("api/receipts")]
public sealed class ReceiptController : ControllerBase
{
    private readonly IReceiptPrintService _receiptPrintService;

    public ReceiptController(IReceiptPrintService receiptPrintService)
    {
        _receiptPrintService = receiptPrintService;
    }

    [HttpPost("print/{receiptId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Print([FromRoute] long receiptId, CancellationToken cancellationToken)
    {
        await _receiptPrintService.PrintReceiptAsync(receiptId, isReprint: false, cancellationToken);
        return NoContent();
    }

    [HttpPost("reprint/{receiptNumber}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reprint([FromRoute] string receiptNumber, CancellationToken cancellationToken)
    {
        await _receiptPrintService.ReprintByReceiptNumberAsync(receiptNumber, cancellationToken);
        return NoContent();
    }
}
