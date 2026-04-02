using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using POS.Application.Abstractions;
using POS.Application.Models;
using POS.Infrastructure.Data;
using POS.Infrastructure.Security;

namespace POS.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly PosDbContext _dbContext;
    private readonly ISystemClock _clock;
    private readonly IOptions<SecurityOptions> _securityOptions;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        PosDbContext dbContext,
        ISystemClock clock,
        IOptions<SecurityOptions> securityOptions,
        IAuditLogService auditLogService,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _securityOptions = securityOptions;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return new LoginResponse
            {
                IsAuthenticated = false,
                Message = "Invalid username or password.",
            };
        }

        var normalizedUsername = request.Username.Trim();
        var user = await _dbContext.Users.SingleOrDefaultAsync(
            x => x.Username.ToLower() == normalizedUsername.ToLower(),
            cancellationToken);

        if (user is null || !user.IsActive)
        {
            await WriteAuditAsync(null, "Login", "Failure", "User not found or inactive.", cancellationToken);
            return new LoginResponse
            {
                IsAuthenticated = false,
                Message = "Invalid username or password.",
            };
        }

        var now = _clock.UtcNow;

        if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc > now)
        {
            await WriteAuditAsync(user.Id, "Login", "Locked", "Account is currently locked.", cancellationToken);
            return new LoginResponse
            {
                IsAuthenticated = false,
                Message = "Account locked due to failed login attempts.",
                LockoutEndUtc = user.LockoutEndUtc,
            };
        }

        var isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (!isPasswordValid)
        {
            user.FailedLoginAttempts += 1;

            if (user.FailedLoginAttempts >= Math.Max(1, _securityOptions.Value.MaxFailedLoginAttempts))
            {
                user.LockoutEndUtc = now.AddMinutes(Math.Max(1, _securityOptions.Value.LockoutMinutes));
                user.FailedLoginAttempts = 0;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(user.Id, "Login", "Failure", "Invalid password.", cancellationToken);

            return new LoginResponse
            {
                IsAuthenticated = false,
                Message = "Invalid username or password.",
                LockoutEndUtc = user.LockoutEndUtc,
            };
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEndUtc = null;
        user.LastLoginUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var token = GenerateToken(user.Id, user.Username, user.Role.ToString());

        await WriteAuditAsync(user.Id, "Login", "Success", null, cancellationToken);

        _logger.LogInformation("User {Username} authenticated successfully.", user.Username);

        return new LoginResponse
        {
            IsAuthenticated = true,
            Message = "Authenticated.",
            Token = token,
            UserId = user.Id,
            Role = user.Role,
        };
    }

    private string GenerateToken(long userId, string username, string role)
    {
        var options = _securityOptions.Value;
        var keyBytes = Encoding.UTF8.GetBytes(options.JwtSigningKey);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var now = _clock.UtcNow;
        var tokenLifetime = options.TokenLifetimeSeconds > 0
            ? TimeSpan.FromSeconds(options.TokenLifetimeSeconds)
            : TimeSpan.FromMinutes(Math.Max(1, options.TokenLifetimeMinutes));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: options.JwtIssuer,
            audience: options.JwtAudience,
            claims: claims,
            notBefore: now,
            expires: now.Add(tokenLifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private Task WriteAuditAsync(long? userId, string action, string status, string? error, CancellationToken cancellationToken)
    {
        return _auditLogService.WriteAsync(
            new AuditLogEntry
            {
                UserId = userId,
                Action = action,
                ResourceType = "User",
                ResourceId = userId?.ToString(),
                Status = status,
                ErrorMessage = error,
            },
            cancellationToken);
    }
}
