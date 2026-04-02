using POS.Application.Models;

namespace POS.Application.Abstractions;

public interface IRefundService
{
    Task<RefundResponse> CreateRefundAsync(RefundRequest request, CancellationToken cancellationToken = default);
}
