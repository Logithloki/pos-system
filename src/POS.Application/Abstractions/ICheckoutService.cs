using POS.Application.Models;

namespace POS.Application.Abstractions;

public interface ICheckoutService
{
    Task<CheckoutResponse> ProcessCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);
}
