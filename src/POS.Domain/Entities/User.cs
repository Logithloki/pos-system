using POS.Domain.Common;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

public sealed class User : EntityBase, IConcurrencyTracked
{
    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public UserRole Role { get; set; } = UserRole.Cashier;

    public bool IsActive { get; set; } = true;

    public int FailedLoginAttempts { get; set; }

    public DateTime? LockoutEndUtc { get; set; }

    public DateTime? LastLoginUtc { get; set; }

    public long Version { get; set; } = 1;
}
