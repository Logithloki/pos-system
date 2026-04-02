using POS.Application.Abstractions;

namespace POS.Infrastructure.Services;

public sealed class NoOpCheckoutExecutionHook : ICheckoutExecutionHook
{
    public Task OnAfterInventoryDeductionAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
