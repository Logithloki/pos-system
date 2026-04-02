using Microsoft.EntityFrameworkCore;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data;

public sealed class PosDbContext : DbContext
{
    public PosDbContext(DbContextOptions<PosDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();

    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();

    public DbSet<Receipt> Receipts => Set<Receipt>();

    public DbSet<ReceiptPrintLog> ReceiptPrintLogs => Set<ReceiptPrintLog>();

    public DbSet<InventoryAdjustment> InventoryAdjustments => Set<InventoryAdjustment>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<BackupRecord> BackupRecords => Set<BackupRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(x => x.Username).HasMaxLength(120).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.HasIndex(x => x.Username).IsUnique();
            entity.Property(x => x.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Contact).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(400).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.CumulativeSpend).HasPrecision(18, 2);
            entity.HasIndex(x => x.Phone).IsUnique();
            entity.Property(x => x.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Barcode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.Property(x => x.Cost).HasPrecision(18, 2);
            entity.HasIndex(x => x.Barcode).IsUnique();
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Products_QuantityOnHand_NonNegative", "QuantityOnHand >= 0");
            });
        });

        modelBuilder.Entity<SalesOrder>(entity =>
        {
            entity.Property(x => x.ReceiptNumber).HasMaxLength(64).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
            entity.Property(x => x.TotalBeforeDiscount).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalAfterTax).HasPrecision(18, 2);
            entity.Property(x => x.AmountTendered).HasPrecision(18, 2);
            entity.HasIndex(x => x.ReceiptNumber).IsUnique();
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.Property(x => x.Version).IsConcurrencyToken();

            entity
                .HasOne(x => x.OriginalSalesOrder)
                .WithMany(x => x.ReversalOrders)
                .HasForeignKey(x => x.OriginalSalesOrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(x => x.Receipt)
                .WithOne(x => x.SalesOrder)
                .HasForeignKey<Receipt>(x => x.SalesOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalesOrderLine>(entity =>
        {
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.LineDiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);

            entity
                .HasOne(x => x.SalesOrder)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.SalesOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(x => x.Product)
                .WithMany(x => x.SalesOrderLines)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.Property(x => x.ReceiptNumber).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ThermalPayload).IsRequired();
            entity.Property(x => x.PayloadHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.ReceiptNumber).IsUnique();
            entity.HasIndex(x => x.SalesOrderId).IsUnique();
            entity.Property(x => x.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<ReceiptPrintLog>(entity =>
        {
            entity.Property(x => x.ErrorMessage).HasMaxLength(500);
            entity
                .HasOne(x => x.Receipt)
                .WithMany(x => x.PrintLogs)
                .HasForeignKey(x => x.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InventoryAdjustment>(entity =>
        {
            entity.Property(x => x.Note).HasMaxLength(500);
            entity
                .HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(x => x.Action).HasMaxLength(120).IsRequired();
            entity.Property(x => x.ResourceType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.ResourceId).HasMaxLength(120);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CorrelationId).HasMaxLength(120);
            entity.Property(x => x.ErrorMessage).HasMaxLength(1000);
        });

        modelBuilder.Entity<BackupRecord>(entity =>
        {
            entity.Property(x => x.FilePath).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ChecksumSha256).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Note).HasMaxLength(500);
        });

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        ApplyEntityPolicies();

        try
        {
            return base.SaveChanges();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            ResetConcurrencyEntries(ex);
            throw;
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyEntityPolicies();

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await ResetConcurrencyEntriesAsync(ex, cancellationToken);
            throw;
        }
    }

    private void ApplyEntityPolicies()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedUtc = utcNow;
                entry.Entity.UpdatedUtc = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedUtc = utcNow;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Receipt>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("Receipt records are immutable and cannot be modified or deleted.");
            }
        }

        foreach (var entry in ChangeTracker.Entries<IConcurrencyTracked>())
        {
            if (entry.State == EntityState.Added && entry.Entity.Version <= 0)
            {
                entry.Entity.Version = 1;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.Version += 1;
            }
        }
    }

    private static void ResetConcurrencyEntries(DbUpdateConcurrencyException exception)
    {
        foreach (var entry in exception.Entries)
        {
            if (entry.Entity is not IConcurrencyTracked)
            {
                continue;
            }

            var databaseValues = entry.GetDatabaseValues();
            if (databaseValues is null)
            {
                entry.State = EntityState.Detached;
                continue;
            }

            entry.CurrentValues.SetValues(databaseValues);
            entry.OriginalValues.SetValues(databaseValues);
            entry.State = EntityState.Unchanged;
        }
    }

    private static async Task ResetConcurrencyEntriesAsync(DbUpdateConcurrencyException exception, CancellationToken cancellationToken)
    {
        foreach (var entry in exception.Entries)
        {
            if (entry.Entity is not IConcurrencyTracked)
            {
                continue;
            }

            var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);
            if (databaseValues is null)
            {
                entry.State = EntityState.Detached;
                continue;
            }

            entry.CurrentValues.SetValues(databaseValues);
            entry.OriginalValues.SetValues(databaseValues);
            entry.State = EntityState.Unchanged;
        }
    }
}
