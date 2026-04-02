using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Abstractions;
using POS.Application.Models;

namespace POS.API.Controllers;

[ApiController]
[Authorize(Policy = "CashierOrAdmin")]
[Route("api/checkout")]
public sealed class CheckoutController : ControllerBase
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
        var response = await _checkoutService.ProcessCheckoutAsync(request, cancellationToken);
        return Ok(response);
    }
}
