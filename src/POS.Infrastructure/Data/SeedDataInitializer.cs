using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Infrastructure.Data;

public sealed class SeedDataInitializer
{
    private readonly PosDbContext _dbContext;
    private readonly ILogger<SeedDataInitializer> _logger;

    public SeedDataInitializer(PosDbContext dbContext, ILogger<SeedDataInitializer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.MigrateAsync(cancellationToken);

        if (await _dbContext.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var adminUsername = Environment.GetEnvironmentVariable("POS_ADMIN_USERNAME");
        var adminPassword = Environment.GetEnvironmentVariable("POS_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(adminUsername) || string.IsNullOrWhiteSpace(adminPassword))
        {
            _logger.LogWarning("Initial admin user was not created because POS_ADMIN_USERNAME and POS_ADMIN_PASSWORD are not set.");
            return;
        }

        var admin = new User
        {
            Username = adminUsername.Trim(),
            FullName = "Initial Administrator",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword, 12),
            Role = UserRole.Admin,
            IsActive = true,
        };

        _dbContext.Users.Add(admin);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Initial admin user created from environment variables.");
    }
}
