using POS.Domain.Enums;

namespace POS.Application.Models;

public sealed class LoginResponse
{
    public bool IsAuthenticated { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? Token { get; init; }

    public long? UserId { get; init; }

    public UserRole? Role { get; init; }

    public DateTime? LockoutEndUtc { get; init; }
}
