using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class Supplier : EntityBase, IConcurrencyTracked
{
    public string Name { get; set; } = string.Empty;

    public string Contact { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    public long Version { get; set; } = 1;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
