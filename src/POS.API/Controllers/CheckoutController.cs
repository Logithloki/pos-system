using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Abstractions;
using POS.Application.Models;

namespace POS.API.Controllers;

[ApiController]
[Authorize(Policy = "CashierOrAdmin")]
[Route("api/checkout")]
public sealed class CheckoutController : PosControllerBase
{
    private readonly ICheckoutService _checkoutService;

    public CheckoutController(ICheckoutService checkoutService)
    {
        _checkoutService = checkoutService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CheckoutResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CheckoutResponse>> ProcessCheckout([FromBody] CheckoutRequest request, CancellationToken cancellationToken)
    {
        var authenticatedUserId = GetAuthenticatedUserId();

        var normalizedRequest = new CheckoutRequest
        {
            UserId = authenticatedUserId,
            CustomerId = request.CustomerId,
            IdempotencyKey = request.IdempotencyKey,
            DiscountAmount = request.DiscountAmount,
            DiscountPercent = request.DiscountPercent,
            TaxRatePercent = request.TaxRatePercent,
            PaymentMethod = request.PaymentMethod,
            AmountTendered = request.AmountTendered,
            Items = request.Items,
        };

        var response = await _checkoutService.ProcessCheckoutAsync(normalizedRequest, cancellationToken);
        return Ok(response);
    }
}
