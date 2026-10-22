using FaultCode.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FaultCode.Infrastructure.Persistence;

public sealed class FleetDbContext(DbContextOptions<FleetDbContext> options) : DbContext(options)
{
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<FleetAssociationRow> Associations => Set<FleetAssociationRow>();
    public DbSet<FleetAuditRow> Audit => Set<FleetAuditRow>();
    public DbSet<FleetReplayRow> Replays => Set<FleetReplayRow>();
    public DbSet<WorkflowRecord> WorkflowRecords => Set<WorkflowRecord>();
    public DbSet<ApiRequestQuotaRow> ApiRequestQuotas => Set<ApiRequestQuotaRow>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        var quota = model.Entity<ApiRequestQuotaRow>();
        quota.ToTable("ApiRequestQuotas");
        quota.HasKey(x => new { x.TenantId, x.IdentityHash, x.Day });
        quota.Property(x => x.TenantId).HasMaxLength(36);
        quota.Property(x => x.IdentityHash).HasMaxLength(64);
        var workflow = model.Entity<WorkflowRecord>();
        workflow.ToTable("WorkflowRecords", table => table.HasCheckConstraint("CK_WorkflowRecord_Json", "ISJSON([Payload]) = 1"));
        workflow.HasKey(x => new { x.TenantId, x.Kind, x.Key });
        workflow.Property(x => x.TenantId).HasMaxLength(36); workflow.Property(x => x.Kind).HasMaxLength(40);
        workflow.Property(x => x.Key).HasMaxLength(64); workflow.Property(x => x.ParentKind).HasMaxLength(40);
        workflow.Property(x => x.ParentKey).HasMaxLength(64); workflow.Property(x => x.SourceClientId).HasMaxLength(36);
        workflow.Property(x => x.SourceEventId).HasMaxLength(100); workflow.Property(x => x.Version).IsConcurrencyToken();
        workflow.HasIndex(x => new { x.TenantId, x.Kind, x.Position });
        workflow.HasIndex(x => new { x.TenantId, x.SourceClientId, x.SourceEventId }).IsUnique()
            .HasFilter("[Kind] = 'incident' AND [SourceClientId] IS NOT NULL AND [SourceEventId] IS NOT NULL");
        workflow.HasOne<Vehicle>().WithMany().HasForeignKey(x => new { x.TenantId, x.VehicleId }).OnDelete(DeleteBehavior.Restrict);
        workflow.HasOne<Driver>().WithMany().HasForeignKey(x => new { x.TenantId, x.DriverId }).OnDelete(DeleteBehavior.Restrict);
        workflow.HasOne<WorkflowRecord>().WithMany().HasForeignKey(x => new { x.TenantId, x.ParentKind, x.ParentKey }).OnDelete(DeleteBehavior.Restrict);
        var drivers = model.Entity<Driver>(); drivers.ToTable("Drivers"); drivers.HasKey(x => new { x.TenantId, x.Id });
        drivers.Property(x => x.TenantId).HasMaxLength(36); drivers.Property(x => x.Identifier).HasMaxLength(40);
        drivers.Property(x => x.OwnerObjectId).HasMaxLength(36); drivers.Property(x => x.DisplayName).HasMaxLength(100);
        drivers.Property(x => x.Email).HasMaxLength(150); drivers.Property(x => x.Version).IsConcurrencyToken();
        drivers.HasIndex(x => new { x.TenantId, x.Identifier }).IsUnique();
        drivers.HasIndex(x => new { x.TenantId, x.OwnerObjectId }).IsUnique();
        var vehicles = model.Entity<Vehicle>(); vehicles.ToTable("Vehicles"); vehicles.HasKey(x => new { x.TenantId, x.Id });
        vehicles.Property(x => x.TenantId).HasMaxLength(36); vehicles.Property(x => x.Identifier).HasMaxLength(40);
        vehicles.Property(x => x.System).HasMaxLength(40); vehicles.Property(x => x.Version).IsConcurrencyToken();
        vehicles.HasIndex(x => new { x.TenantId, x.Identifier }).IsUnique();
        var associations = model.Entity<FleetAssociationRow>(); associations.ToTable("DriverVehicleAssociations");
        associations.HasKey(x => new { x.TenantId, x.Id }); associations.Property(x => x.TenantId).HasMaxLength(36);
        associations.HasOne<Driver>().WithMany().HasForeignKey(x => new { x.TenantId, x.DriverId }).OnDelete(DeleteBehavior.Restrict);
        associations.HasOne<Vehicle>().WithMany().HasForeignKey(x => new { x.TenantId, x.VehicleId }).OnDelete(DeleteBehavior.Restrict);
        associations.HasIndex(x => new { x.TenantId, x.VehicleId, x.DriverId }).IsUnique().HasFilter("[Until] IS NULL");
        var audit = model.Entity<FleetAuditRow>(); audit.ToTable("FleetAudit"); audit.HasKey(x => x.Id);
        audit.Property(x => x.TenantId).HasMaxLength(36); audit.Property(x => x.ActorId).HasMaxLength(36);
        audit.Property(x => x.ClientId).HasMaxLength(36); audit.Property(x => x.CorrelationId).HasMaxLength(256);
        audit.Property(x => x.Operation).HasMaxLength(128); audit.HasIndex(x => new { x.TenantId, x.Id });
        var replays = model.Entity<FleetReplayRow>(); replays.ToTable("FleetReplays"); replays.HasKey(x => new { x.TenantId, x.KeyHash });
        replays.Property(x => x.TenantId).HasMaxLength(36); replays.Property(x => x.KeyHash).HasMaxLength(64);
        replays.Property(x => x.PayloadHash).HasMaxLength(64);
    }
}
public sealed class ApiRequestQuotaRow
{
    public string TenantId { get; set; } = "";
    public string IdentityHash { get; set; } = "";
    public DateOnly Day { get; set; }
    public int Used { get; set; }
}
public sealed class FleetAssociationRow
{
    public string TenantId { get; set; } = "";
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public Guid DriverId { get; set; }
    public DateTimeOffset From { get; set; }
    public DateTimeOffset? Until { get; set; }
}
public sealed class FleetAuditRow
{
    public long Id { get; set; }
    public string TenantId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public string Operation { get; set; } = "";
    public Guid ResourceId { get; set; }
    public DateTimeOffset At { get; set; }
}
public sealed class FleetReplayRow
{
    public string TenantId { get; set; } = "";
    public string KeyHash { get; set; } = "";
    public string Key { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public Guid ResourceId { get; set; }
    public long Version { get; set; }
}
// No connection or credentials are needed to generate migrations; applying them requires an explicit secret.
public sealed class FleetDbContextFactory : IDesignTimeDbContextFactory<FleetDbContext>
{
    public FleetDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<FleetDbContext>()
        .UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__FaultCode")
            ?? "Server=localhost;Database=FaultCode;Integrated Security=true;Encrypt=true").Options);
}

// One versioned typed aggregate or journal entry per row; immutable evidence/history remains in its JSON document.
// Relational keys enforce tenant, source replay, fleet references and notification -> incident integrity.
public sealed class WorkflowRecord
{
    public string TenantId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Key { get; set; } = "";
    public int Position { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public long Version { get; set; }
    public string Payload { get; set; } = "";
    public Guid? VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public string? ParentKind { get; set; }
    public string? ParentKey { get; set; }
    public string? SourceClientId { get; set; }
    public string? SourceEventId { get; set; }
}
