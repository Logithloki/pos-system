using POS.Domain.Enums;

namespace POS.Desktop.Models;

public sealed class TerminalBootstrapData
{
    public required long OperatorUserId { get; init; }

    public required string OperatorDisplayName { get; init; }

    public required UserRole OperatorRole { get; init; }

    public required IReadOnlyCollection<CatalogProduct> Products { get; init; }

    public decimal DefaultTaxRatePercent { get; init; }
}
