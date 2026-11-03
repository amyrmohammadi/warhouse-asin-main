using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;
using Warehouse.Infrastructure.Persistence;
using Xunit;

namespace Warehouse.IntegrationTests.Persistence;

public sealed class EntityAuditPropertiesTests(AuditDatabaseFixture database) : IClassFixture<AuditDatabaseFixture>
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task SaveOverloads_PersistActorAndTime_AndProtectCreationProperties(int overload)
    {
        var actor = new TestActor();
        var creator = Guid.NewGuid();
        actor.SetCurrentUser(creator, "Creator");
        var clock = new TestClock();
        var createdOn = clock.GetUtcNow();
        await using var context = database.CreateContext(actor, clock);
        var unit = new UnitOfMeasure($"Unit-{Guid.NewGuid():N}", null);
        context.Add(unit);
        context.Entry(unit).Property(item => item.CreatedBy).CurrentValue = "spoofed";
        context.Entry(unit).Property(item => item.CreatedOn).CurrentValue = createdOn.AddYears(-1);
        await SaveAsync(context, overload);
        context.ChangeTracker.Clear();
        unit = await context.UnitsOfMeasure.SingleAsync(item => item.Id == unit.Id);
        Assert.Equal(createdOn, unit.CreatedOn);
        Assert.Equal(creator.ToString(), unit.CreatedBy);
        Assert.Equal(createdOn, unit.LastModifiedOn);
        Assert.Equal(creator.ToString(), unit.LastModifiedBy);

        var editor = Guid.NewGuid();
        actor.SetCurrentUser(editor, "Editor");
        clock.Now = createdOn.AddHours(1);
        unit.Update(unit.Title, "Updated");
        context.Entry(unit).Property(item => item.CreatedBy).CurrentValue = "spoofed-again";
        context.Entry(unit).Property(item => item.CreatedOn).CurrentValue = createdOn.AddYears(-2);
        await SaveAsync(context, overload);
        context.ChangeTracker.Clear();
        unit = await context.UnitsOfMeasure.SingleAsync(item => item.Id == unit.Id);
        Assert.Equal(createdOn, unit.CreatedOn);
        Assert.Equal(creator.ToString(), unit.CreatedBy);
        Assert.Equal(clock.Now, unit.LastModifiedOn);
        Assert.Equal(editor.ToString(), unit.LastModifiedBy);

        var audits = await context.AuditTrails.Where(audit => audit.TableName == "UnitsOfMeasure" &&
            audit.PrimaryKey.Contains(unit.Id.ToString())).OrderBy(audit => audit.Id).ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.Equal(creator.ToString(), audits[0].CreatedBy);
        Assert.Equal(createdOn, audits[0].CreatedOn);
        Assert.Equal(editor.ToString(), audits[1].CreatedBy);
        Assert.Equal(clock.Now, audits[1].CreatedOn);
        Assert.All(audits, audit =>
        {
            Assert.Equal(audit.CreatedBy, audit.LastModifiedBy);
            Assert.Equal(audit.CreatedOn, audit.LastModifiedOn);
        });
        Assert.DoesNotContain(nameof(AuditableEntity.LastModifiedOn), audits[1].ChangedColumns!);

        clock.Now = clock.Now.AddHours(1);
        Assert.Equal(0, await context.SaveChangesAsync());
        Assert.Equal(createdOn.AddHours(1), unit.LastModifiedOn);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnonymousSave_LeavesActorFieldsNull_AndPreservesOriginalCreator(bool authenticatedCreation)
    {
        var actor = new TestActor();
        var creator = Guid.NewGuid();
        if (authenticatedCreation) actor.SetCurrentUser(creator, "Creator");
        var clock = new TestClock();
        await using var context = database.CreateContext(actor, clock);
        var unit = new UnitOfMeasure($"Unit-{Guid.NewGuid():N}", null);
        context.Add(unit);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        unit = await context.UnitsOfMeasure.SingleAsync(item => item.Id == unit.Id);
        var createdOn = unit.CreatedOn;
        Assert.Equal(authenticatedCreation ? creator.ToString() : null, unit.CreatedBy);
        Assert.Equal(unit.CreatedBy, unit.LastModifiedBy);

        actor.SetSystem();
        clock.Now = clock.Now.AddDays(1);
        unit.Update(unit.Title, "Anonymous edit");
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        unit = await context.UnitsOfMeasure.SingleAsync(item => item.Id == unit.Id);
        Assert.Equal(authenticatedCreation ? creator.ToString() : null, unit.CreatedBy);
        Assert.Equal(createdOn, unit.CreatedOn);
        Assert.Null(unit.LastModifiedBy);
        Assert.Equal(clock.Now, unit.LastModifiedOn);
    }

    [Fact]
    public async Task ContextWithoutActor_PersistsNullUserIds()
    {
        await using var context = database.CreateContext();
        var unit = new UnitOfMeasure($"Unit-{Guid.NewGuid():N}", null);
        context.Add(unit);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var stored = await context.UnitsOfMeasure.SingleAsync(item => item.Id == unit.Id);
        Assert.Null(stored.CreatedBy);
        Assert.Null(stored.LastModifiedBy);
        Assert.NotNull(stored.CreatedOn);
        Assert.Equal(stored.CreatedOn, stored.LastModifiedOn);
    }

    [Fact]
    public async Task DetachedUpdate_PreservesCreationValuesInDatabase()
    {
        var actor = new TestActor();
        var creator = Guid.NewGuid();
        actor.SetCurrentUser(creator, "Creator");
        var clock = new TestClock();
        await using var context = database.CreateContext(actor, clock);
        var unit = new UnitOfMeasure($"Unit-{Guid.NewGuid():N}", null);
        context.Add(unit);
        await context.SaveChangesAsync();
        var createdOn = unit.CreatedOn;
        context.ChangeTracker.Clear();
        unit = await context.UnitsOfMeasure.AsNoTracking().SingleAsync(item => item.Id == unit.Id);
        unit.Update(unit.Title, "Detached edit");
        context.Update(unit);
        context.Entry(unit).Property(item => item.CreatedOn).CurrentValue = null;
        context.Entry(unit).Property(item => item.CreatedBy).CurrentValue = "spoofed";
        clock.Now = clock.Now.AddHours(1);
        actor.SetSystem();
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        unit = await context.UnitsOfMeasure.SingleAsync(item => item.Id == unit.Id);
        Assert.Equal(createdOn, unit.CreatedOn);
        Assert.Equal(creator.ToString(), unit.CreatedBy);
        Assert.Equal(clock.Now, unit.LastModifiedOn);
        Assert.Null(unit.LastModifiedBy);
    }

    [Fact]
    public async Task AuditForDatabaseGeneratedKey_AlsoReceivesAuditProperties()
    {
        var actor = new TestActor();
        actor.SetCurrentUser(Guid.NewGuid(), "Actor");
        var clock = new TestClock();
        await using var context = database.CreateContext(actor, clock);
        var role = new IdentityRole<Guid>($"Role-{Guid.NewGuid():N}") { Id = Guid.NewGuid() };
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        var claim = new IdentityRoleClaim<Guid> { RoleId = role.Id, ClaimType = "Test", ClaimValue = Guid.NewGuid().ToString() };
        context.RoleClaims.Add(claim);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var audit = await context.AuditTrails.SingleAsync(item => item.TableName == "RoleClaims" &&
            item.PrimaryKey == "{\"Id\":" + claim.Id + "}");
        Assert.Equal(clock.Now, audit.CreatedOn);
        Assert.Equal(clock.Now, audit.LastModifiedOn);
        Assert.Equal(actor.UserId.ToString(), audit.CreatedBy);
        Assert.Equal(actor.UserId.ToString(), audit.LastModifiedBy);
    }

    [Fact]
    public void Model_AddsFourScalarPropertiesToEveryNonIdentityTable_WithoutUserRelationships()
    {
        using var context = database.CreateContext();
        Assert.False(context.Database.HasPendingModelChanges());
        foreach (var entity in context.Model.GetEntityTypes())
        {
            string[] properties = [nameof(AuditableEntity.CreatedOn), nameof(AuditableEntity.CreatedBy),
                nameof(AuditableEntity.LastModifiedOn), nameof(AuditableEntity.LastModifiedBy)];
            if (entity.GetSchema() == "Identity")
            {
                Assert.All(properties, name => Assert.Null(entity.FindProperty(name)));
                continue;
            }
            Assert.All(properties, name => Assert.NotNull(entity.FindProperty(name)));
            Assert.Equal(typeof(string), entity.FindProperty(nameof(AuditableEntity.CreatedBy))!.ClrType);
            Assert.Equal(typeof(string), entity.FindProperty(nameof(AuditableEntity.LastModifiedBy))!.ClrType);
            Assert.DoesNotContain(entity.GetForeignKeys(), key => key.Properties.Any(property => properties.Contains(property.Name)));
        }
    }

    private static async Task SaveAsync(ApplicationDbContext context, int overload)
    {
        switch (overload)
        {
            case 0: context.SaveChanges(); break;
            case 1: context.SaveChanges(true); break;
            case 2: context.SaveChanges(false); break;
            case 3: await context.SaveChangesAsync(); break;
            case 4: await context.SaveChangesAsync(true); break;
            case 5: await context.SaveChangesAsync(false); break;
        }
        context.ChangeTracker.AcceptAllChanges();
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class TestActor : ICurrentUserContext
    {
        public Guid? UserId { get; private set; }
        public string UserName { get; private set; } = "System";
        public void SetCurrentUser(Guid userId, string? fullName) { UserId = userId; UserName = fullName ?? ""; }
        public void SetSystem() { UserId = null; UserName = "System"; }
    }
}

public sealed class AuditDatabaseFixture : IAsyncLifetime
{
    private readonly DbContextOptions<ApplicationDbContext> _options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database=Warehouse_AuditTests_{Guid.NewGuid():N};" +
            "Trusted_Connection=True;TrustServerCertificate=True")
        .Options;

    public ApplicationDbContext CreateContext(ICurrentUserContext? actor = null, TimeProvider? clock = null) =>
        new(_options, actor, clock ?? TimeProvider.System);

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }
}
