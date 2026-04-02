using POS.Application.Models;
using POS.Desktop.Models;

namespace POS.Desktop.Services;

public interface ICashierTerminalService
{
    Task<TerminalBootstrapData> InitializeAsync(CancellationToken cancellationToken = default);

    Task<CatalogProduct> QuickAddProductAsync(QuickAddProductRequest request, CancellationToken cancellationToken = default);

    Task<CheckoutResponse> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);

    Task PrintReceiptAsync(string receiptNumber, CancellationToken cancellationToken = default);
}
