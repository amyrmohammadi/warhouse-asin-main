using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;
using Warehouse.Infrastructure.Identity;
using Warehouse.Infrastructure.Persistence.Auditing;

namespace Warehouse.Infrastructure.Persistence;

public sealed class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ICurrentUserContext? _currentUserContext;
    private readonly TimeProvider _timeProvider;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : this(options, null, TimeProvider.System)
    {
    }

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserContext? currentUserContext,
        TimeProvider timeProvider)
        : base(options)
    {
        _currentUserContext = currentUserContext;
        _timeProvider = timeProvider;
    }

    public DbSet<AuditTrail> AuditTrails => Set<AuditTrail>();

    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<StorageWarehouse> Warehouses => Set<StorageWarehouse>();

    public DbSet<Person> People => Set<Person>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();

    public DbSet<GoodsReceiptItem> GoodsReceiptItems => Set<GoodsReceiptItem>();

    public DbSet<GoodsIssueDestination> GoodsIssueDestinations => Set<GoodsIssueDestination>();

    public DbSet<GoodsIssue> GoodsIssues => Set<GoodsIssue>();

    public DbSet<GoodsIssueItem> GoodsIssueItems => Set<GoodsIssueItem>();

    internal DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public override int SaveChanges() => SaveChanges(acceptAllChangesOnSuccess: true);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var pendingAuditEntries = OnBeforeSaveChanges();
        var result = base.SaveChanges(acceptAllChangesOnSuccess);

        if (pendingAuditEntries.Count > 0)
        {
            OnAfterSaveChanges(pendingAuditEntries);
            base.SaveChanges(acceptAllChangesOnSuccess);
        }

        return result;
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default) =>
        SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        var pendingAuditEntries = OnBeforeSaveChanges();
        var result = await base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);

        if (pendingAuditEntries.Count > 0)
        {
            OnAfterSaveChanges(pendingAuditEntries);
            await base.SaveChangesAsync(
                acceptAllChangesOnSuccess,
                cancellationToken);
        }

        return result;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("Identity");
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(user => user.FullName).HasMaxLength(200);
            entity.Property(user => user.ProfileImage).HasColumnType("varbinary(max)");
        });
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(token => token.Id);
            entity.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(token => token.SecurityStamp).HasMaxLength(256).IsRequired();
            entity.Property(token => token.ConcurrencyStamp).HasMaxLength(256).IsRequired();
            entity.Property(token => token.ReplacedByTokenHash).HasMaxLength(64);
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasIndex(token => new { token.UserId, token.RevokedAtUtc });
            entity.HasOne(token => token.User)
                .WithMany()
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditTrail>(entity =>
        {
            entity.ToTable("AuditTrails", "Audit");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Action)
                .HasConversion<string>()
                .HasMaxLength(16);
            entity.Property(audit => audit.TableName).HasMaxLength(128).IsRequired();
            entity.Property(audit => audit.UserName)
                .HasMaxLength(256)
                .HasDefaultValue("System")
                .IsRequired();
            entity.Property(audit => audit.PrimaryKey).IsRequired();
            entity.HasIndex(audit => audit.OccurredAtUtc);
            entity.HasIndex(audit => new { audit.UserId, audit.OccurredAtUtc });
            entity.HasIndex(audit => new { audit.TableName, audit.OccurredAtUtc });
        });

        builder.Entity<UnitOfMeasure>(entity =>
        {
            entity.ToTable("UnitsOfMeasure", "Business");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ConcurrencyStamp)
                .HasDefaultValueSql("NEWID()")
                .IsConcurrencyToken();
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(1000);
            entity.HasIndex(item => item.Title);
        });

        builder.Entity<Supplier>(entity =>
        {
            entity.ToTable("Suppliers", "Business");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ConcurrencyStamp)
                .HasDefaultValueSql("NEWID()")
                .IsConcurrencyToken();
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(1000);
            entity.HasIndex(item => item.Title);
        });

        builder.Entity<Invoice>(entity =>
        {
            entity.ToTable("Invoices", "Business");
            entity.HasKey(invoice => invoice.Id);
            entity.Property(invoice => invoice.InvoiceNumber).HasMaxLength(100).IsRequired();
            entity.Property(invoice => invoice.Description).HasMaxLength(1000);
            entity.Property(invoice => invoice.InvoiceDate).HasColumnType("date");
            entity.Property(invoice => invoice.ProcessingStatus)
                .HasConversion<string>()
                .HasMaxLength(32)
                .HasDefaultValue(InvoiceProcessingStatus.UnderReview)
                .IsRequired();
            entity.HasIndex(invoice => new { invoice.InvoiceNumber, invoice.SupplierId }).IsUnique();
            entity.HasIndex(invoice => invoice.SupplierId);
            entity.HasOne(invoice => invoice.Supplier)
                .WithMany(supplier => supplier.Invoices)
                .HasForeignKey(invoice => invoice.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StorageWarehouse>(entity =>
        {
            entity.ToTable("Warehouses", "Business");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ConcurrencyStamp)
                .HasDefaultValueSql("NEWID()")
                .IsConcurrencyToken();
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(1000);
            entity.HasIndex(item => item.Title);
        });

        builder.Entity<Person>(entity =>
        {
            entity.ToTable("People", "Business");
            entity.HasKey(person => person.Id);
            entity.Property(person => person.ConcurrencyStamp)
                .HasDefaultValueSql("NEWID()")
                .IsConcurrencyToken();
            entity.Property(person => person.FullName).HasMaxLength(200).IsRequired();
            entity.Property(person => person.NormalizedFullName).HasMaxLength(200).IsRequired();
            entity.Property(person => person.PhoneNumber).HasMaxLength(32).IsRequired();
            entity.HasIndex(person => person.NormalizedFullName).IsUnique();
            entity.HasIndex(person => person.FullName);
            entity.HasIndex(person => person.PhoneNumber);
        });

        builder.Entity<Product>(entity =>
        {
            entity.ToTable("Products", "Business");
            entity.HasKey(product => product.Id);
            entity.Property(product => product.ConcurrencyStamp)
                .HasDefaultValueSql("NEWID()")
                .IsConcurrencyToken();
            entity.Property(product => product.Title).HasMaxLength(200).IsRequired();
            entity.Property(product => product.NormalizedTitle).HasMaxLength(200).IsRequired();
            entity.Property(product => product.Code).HasMaxLength(100).IsRequired();
            entity.Property(product => product.NormalizedCode).HasMaxLength(100).IsRequired();
            entity.Property(product => product.Description).HasMaxLength(1000);
            entity.HasIndex(product => product.NormalizedCode)
                .IsUnique()
                .HasFilter("[Code] <> N'-'");
            entity.HasIndex(product => product.NormalizedTitle);
            entity.HasIndex(product => product.Title);
            entity.HasIndex(product => product.UnitOfMeasureId);
            entity.HasOne(product => product.UnitOfMeasure)
                .WithMany()
                .HasForeignKey(product => product.UnitOfMeasureId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GoodsReceipt>(entity =>
        {
            entity.ToTable("GoodsReceipts", "Business");
            entity.HasKey(receipt => receipt.Id);
            entity.Property(receipt => receipt.ConcurrencyStamp)
                .HasDefaultValueSql("NEWID()")
                .IsConcurrencyToken();
            entity.Property(receipt => receipt.Description).HasMaxLength(1000);
            entity.Property(receipt => receipt.PageNumber).HasMaxLength(100).IsRequired();
            entity.HasIndex(receipt => receipt.EnteredAtUtc);
            entity.HasIndex(receipt => receipt.PageNumber);
            entity.HasIndex(receipt => receipt.DelivererId);
            entity.HasOne(receipt => receipt.Deliverer)
                .WithMany(person => person.GoodsReceipts)
                .HasForeignKey(receipt => receipt.DelivererId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GoodsReceiptItem>(entity =>
        {
            entity.ToTable("GoodsReceiptItems", "Business");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Quantity).HasPrecision(18, 3).IsRequired();
            entity.Property(item => item.SortOrder).IsRequired();
            entity.Property(item => item.RequestNumber).HasMaxLength(100);
            entity.Property(item => item.Description).HasMaxLength(1000);
            entity.Property(item => item.InvoiceStatus)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired(false);
            entity.Property(item => item.ReceiptNumber).HasMaxLength(100);
            entity.HasIndex(item => item.ProductId);
            entity.HasIndex(item => item.WarehouseId);
            entity.HasIndex(item => item.InvoiceId);
            entity.HasIndex(item => item.GoodsIssueItemId);
            entity.HasIndex(item => new { item.GoodsReceiptId, item.ProductId });
            entity.HasOne(item => item.GoodsReceipt)
                .WithMany(receipt => receipt.Items)
                .HasForeignKey(item => item.GoodsReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Product)
                .WithMany(product => product.GoodsReceiptItems)
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Warehouse)
                .WithMany(warehouse => warehouse.GoodsReceiptItems)
                .HasForeignKey(item => item.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Invoice)
                .WithMany(invoice => invoice.GoodsReceiptItems)
                .HasForeignKey(item => item.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.GoodsIssueItem)
                .WithMany(issueItem => issueItem.GoodsReceiptItems)
                .HasForeignKey(item => item.GoodsIssueItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GoodsIssueDestination>(entity =>
        {
            entity.ToTable("GoodsIssueDestinations", "Business");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ConcurrencyStamp)
                .HasDefaultValueSql("NEWID()")
                .IsConcurrencyToken();
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(1000);
            entity.HasIndex(item => item.Title).IsUnique();
        });

        builder.Entity<GoodsIssue>(entity =>
        {
            entity.ToTable("GoodsIssues", "Business");
            entity.HasKey(issue => issue.Id);
            entity.Property(issue => issue.ConcurrencyStamp)
                .HasDefaultValueSql("NEWID()")
                .IsConcurrencyToken();
            entity.Property(issue => issue.IssueNumber).HasMaxLength(100).IsRequired();
            entity.Property(issue => issue.Description).HasMaxLength(1000);
            entity.HasIndex(issue => issue.IssueNumber).IsUnique();
            entity.HasIndex(issue => issue.IssuedAtUtc);
            entity.HasIndex(issue => issue.IssuerId);
            entity.HasOne(issue => issue.Issuer)
                .WithMany(person => person.GoodsIssues)
                .HasForeignKey(issue => issue.IssuerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GoodsIssueItem>(entity =>
        {
            entity.ToTable("GoodsIssueItems", "Business");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ProductCode).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Quantity).HasPrecision(18, 3).IsRequired();
            entity.Property(item => item.AssetNumber).HasMaxLength(100);
            entity.Property(item => item.ExitReason).HasMaxLength(1000);
            entity.Property(item => item.ExitReasonType)
                .HasConversion<string>()
                .HasMaxLength(32)
                .HasDefaultValue(GoodsIssueExitReason.Other)
                .IsRequired();
            entity.Property(item => item.IsReturnable).HasDefaultValue(true).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(1000);
            entity.Property(item => item.DeliveringUnit).HasMaxLength(200);
            entity.Property(item => item.SortOrder).IsRequired();
            entity.HasIndex(item => item.GoodsIssueId);
            entity.HasIndex(item => item.ProductId);
            entity.HasIndex(item => item.DestinationId);
            entity.HasIndex(item => item.ShippingLocationId);
            entity.HasOne(item => item.GoodsIssue)
                .WithMany(issue => issue.Items)
                .HasForeignKey(item => item.GoodsIssueId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Product)
                .WithMany(product => product.GoodsIssueItems)
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Destination)
                .WithMany(destination => destination.GoodsIssueItems)
                .HasForeignKey(item => item.DestinationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ShippingLocation)
                .WithMany(supplier => supplier.GoodsIssueShippingItems)
                .HasForeignKey(item => item.ShippingLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

    }

    private List<AuditEntry> OnBeforeSaveChanges()
    {
        ChangeTracker.DetectChanges();
        var auditEntries = new List<AuditEntry>();

        foreach (var entry in ChangeTracker.Entries().ToArray())
        {
            if (entry.Entity is AuditTrail ||
                entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            var auditEntry = new AuditEntry(entry)
            {
                UserId = _currentUserContext?.UserId,
                UserName = _currentUserContext?.UserName ?? "System",
                TableName = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name,
                Action = entry.State switch
                {
                    EntityState.Added => AuditAction.Create,
                    EntityState.Modified => AuditAction.Update,
                    EntityState.Deleted => AuditAction.Delete,
                    _ => throw new InvalidOperationException(
                        $"Entity state '{entry.State}' cannot be audited.")
                },
                OccurredAtUtc = _timeProvider.GetUtcNow()
            };

            foreach (var property in entry.Properties)
            {
                var propertyName = property.Metadata.Name;
                if (propertyName == "ConcurrencyStamp") continue;

                if (property.IsTemporary)
                {
                    auditEntry.TemporaryProperties.Add(property);
                    continue;
                }

                if (property.Metadata.IsPrimaryKey())
                {
                    auditEntry.KeyValues[propertyName] = property.CurrentValue;
                    continue;
                }

                switch (entry.State)
                {
                    case EntityState.Added:
                        auditEntry.NewValues[propertyName] =
                            auditEntry.Sanitize(propertyName, property.CurrentValue);
                        break;

                    case EntityState.Deleted:
                        auditEntry.OldValues[propertyName] =
                            auditEntry.Sanitize(propertyName, property.OriginalValue);
                        break;

                    case EntityState.Modified
                        when property.IsModified &&
                             !Equals(property.OriginalValue, property.CurrentValue):
                        auditEntry.ChangedColumns.Add(propertyName);
                        auditEntry.OldValues[propertyName] =
                            auditEntry.Sanitize(propertyName, property.OriginalValue);
                        auditEntry.NewValues[propertyName] =
                            auditEntry.Sanitize(propertyName, property.CurrentValue);
                        break;
                }
            }

            if (entry.State != EntityState.Modified ||
                auditEntry.ChangedColumns.Count > 0)
            {
                auditEntries.Add(auditEntry);
            }
        }

        foreach (var auditEntry in auditEntries.Where(entry => !entry.HasTemporaryProperties))
        {
            AuditTrails.Add(auditEntry.ToAuditTrail());
        }

        return auditEntries.Where(entry => entry.HasTemporaryProperties).ToList();
    }

    private void OnAfterSaveChanges(IEnumerable<AuditEntry> auditEntries)
    {
        foreach (var auditEntry in auditEntries)
        {
            foreach (var property in auditEntry.TemporaryProperties)
            {
                var propertyName = property.Metadata.Name;
                if (property.Metadata.IsPrimaryKey())
                {
                    auditEntry.KeyValues[propertyName] = property.CurrentValue;
                }
                else
                {
                    auditEntry.NewValues[propertyName] =
                        auditEntry.Sanitize(propertyName, property.CurrentValue);
                }
            }

            AuditTrails.Add(auditEntry.ToAuditTrail());
        }
    }
}
