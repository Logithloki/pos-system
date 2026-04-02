namespace POS.Application.Abstractions;

public interface ICheckoutExecutionHook
{
    Task OnAfterInventoryDeductionAsync(CancellationToken cancellationToken = default);
}
