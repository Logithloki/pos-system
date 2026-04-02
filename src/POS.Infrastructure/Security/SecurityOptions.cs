namespace POS.Infrastructure.Security;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public string JwtSigningKey { get; set; } = string.Empty;

    public string JwtIssuer { get; set; } = "POS.Local";

    public string JwtAudience { get; set; } = "POS.Desktop";

    public int TokenLifetimeMinutes { get; set; } = 30;

    public int TokenLifetimeSeconds { get; set; }

    public int MaxFailedLoginAttempts { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 15;
}
