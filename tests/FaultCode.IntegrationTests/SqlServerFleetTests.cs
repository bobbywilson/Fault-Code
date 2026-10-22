using FaultCode.Application.Demo;
using FaultCode.Application.Fleet;
using FaultCode.Domain;
using FaultCode.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace FaultCode.IntegrationTests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FAULTCODE_SQL_TEST_CONNECTION")))
            Skip = "Real SQL Server connection not configured; no mock substitutes for database integration.";
    }
}
public sealed class SqlServerFleetTests
{
    [SqlServerFact]
    public async Task Real_sql_fault_seed_is_idempotent_and_catalog_survives_reconnect()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>().UseSqlServer(Environment.GetEnvironmentVariable("FAULTCODE_SQL_TEST_CONNECTION")).Options;
        await using (var db = new FleetDbContext(options)) await db.Database.MigrateAsync();
        var caller = new FaultCode.Application.Knowledge.KnowledgeCaller(Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"), "demo-seed-test");
        var first = new SqlServerWorkflowStore(options);
        var seeder = new DemoFaultSeeder(first, TimeProvider.System);
        using (var manifest = typeof(FaultCode.Api.Controllers.IncidentsController).Assembly.GetManifestResourceStream("FaultCode.DemoFaultCatalog.json")!)
            Assert.Equal(9, await seeder.SeedAsync(caller, manifest, default));
        var reconnected = new SqlServerWorkflowStore(options);
        using (var manifest = typeof(FaultCode.Api.Controllers.IncidentsController).Assembly.GetManifestResourceStream("FaultCode.DemoFaultCatalog.json")!)
            Assert.Equal(0, await new DemoFaultSeeder(reconnected, TimeProvider.System).SeedAsync(caller, manifest, default));
        var state = await ((FaultCode.Application.Knowledge.IKnowledgeStore)reconnected).ReadAsync(caller.TenantId, default);
        Assert.Equal(9, state.Faults.Count); Assert.Equal(9, state.Audit.Count);
        Assert.All(state.Faults, f => { Assert.Equal(caller.TenantId, f.TenantId); Assert.Empty(f.Publications); Assert.All(f.Draft.Remedies, r => Assert.False(r.Approved)); });
        Assert.Empty((await ((FaultCode.Application.Knowledge.IKnowledgeStore)reconnected).ReadAsync(Guid.NewGuid().ToString("D"), default)).Faults);
    }


    [Fact]
    public void Sql_server_model_and_migration_include_normalized_tenant_bound_many_to_many_constraints()
    {
        using var db = new FleetDbContextFactory().CreateDbContext([]);
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", db.Database.ProviderName);
        var model = db.GetService<IDesignTimeModel>().Model;
        Assert.Equal(2, model.FindEntityType(typeof(Driver))!.FindPrimaryKey()!.Properties.Count);
        var association = model.FindEntityType(typeof(FleetAssociationRow))!;
        Assert.Equal(2, association.GetForeignKeys().Count());
        Assert.Contains(association.GetIndexes(), i => i.IsUnique && i.GetFilter() == "[Until] IS NULL");
        var sql = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE [Drivers]", sql);
        Assert.Contains("CREATE TABLE [Vehicles]", sql);
        Assert.Contains("CREATE TABLE [DriverVehicleAssociations]", sql);
        Assert.Contains("CREATE TABLE [FleetReplays]", sql);
        Assert.Contains("CREATE TABLE [FleetAudit]", sql);
    }

    [SqlServerFact]
    public async Task Real_sql_dispatch_commits_both_records_and_replays_after_reconnect()
    {
        var connection = Environment.GetEnvironmentVariable("FAULTCODE_SQL_TEST_CONNECTION")!;
        Assert.StartsWith("FaultCode_Test_", new SqlConnectionStringBuilder(connection).InitialCatalog);
        var options = new DbContextOptionsBuilder<FleetDbContext>().UseSqlServer(connection).Options;
        await using (var db = new FleetDbContext(options)) await db.Database.MigrateAsync();
        var tenant = Guid.NewGuid().ToString("D");
        var caller = new FleetCaller(tenant, Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"), "sql-dispatch", true, false, true, false);
        var store = new SqlServerWorkflowStore(options);
        await new DemoFleetSeeder(new FleetService(store, TimeProvider.System)).SeedAsync(caller, default);
        var fleet = await ((IFleetStore)store).ReadAsync(tenant, default);
        var dispatch = new FaultCode.Application.Incidents.VehicleFaultDispatchService(store, TimeProvider.System, FaultCode.Application.Telemetry.NullTelemetry.Instance);
        var first = await dispatch.Dispatch(caller, fleet.Vehicles[0].Id, "SYN-EPS:ASSIST-SIGNAL", "sql-event", default);
        var reconnected = new SqlServerWorkflowStore(options);
        var replay = await new FaultCode.Application.Incidents.VehicleFaultDispatchService(reconnected, TimeProvider.System, FaultCode.Application.Telemetry.NullTelemetry.Instance)
            .Dispatch(caller, fleet.Vehicles[0].Id, "SYN-EPS:ASSIST-SIGNAL", "sql-event", default);
        Assert.Equal(first, replay);
        var incidents = await ((FaultCode.Application.Incidents.IIncidentStore)reconnected).ReadAsync(tenant, default);
        Assert.Equal(first.IncidentId, Assert.Single(incidents.Incidents).Id);
        await ((FaultCode.Application.Notifications.INotificationStore)reconnected).Execute(tenant, tx =>
        {
            var notification = Assert.Single(tx.Notifications.Messages);
            Assert.Equal(first.NotificationId, notification.Id); Assert.Equal(first.IncidentId, notification.IncidentId);
            Assert.Equal("AwaitingAgent", notification.State); return 0;
        }, default);
        Assert.Empty((await ((FaultCode.Application.Incidents.IIncidentStore)reconnected).ReadAsync(Guid.NewGuid().ToString("D"), default)).Incidents);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ((FaultCode.Application.Notifications.INotificationStore)reconnected).Execute<int>(tenant, tx =>
        { tx.Notifications.Messages.Clear(); throw new InvalidOperationException("rollback"); }, default));
        Assert.Equal(1, await ((FaultCode.Application.Notifications.INotificationStore)new SqlServerWorkflowStore(options)).Execute(tenant, tx => tx.Notifications.Messages.Count, default));
    }

    [SqlServerFact]
    public async Task Real_sql_server_seed_replay_reconnect_tenant_isolation_and_rollback()
    {
        // Must target an explicitly created, disposable Fault Code test database. Never delete it here.
        var connection = Environment.GetEnvironmentVariable("FAULTCODE_SQL_TEST_CONNECTION")!;
        var settings = new SqlConnectionStringBuilder(connection);
        Assert.StartsWith("FaultCode_Test_", settings.InitialCatalog);
        var options = new DbContextOptionsBuilder<FleetDbContext>().UseSqlServer(connection).Options;
        await using (var db = new FleetDbContext(options)) await db.Database.MigrateAsync();
        var tenant = Guid.NewGuid().ToString("D");
        var caller = new FleetCaller(tenant, Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"), "sql-live", true, false, true, false);
        var store = new SqlServerFleetStore(options); var fleet = new FleetService(store, TimeProvider.System);
        var seeder = new DemoFleetSeeder(fleet);
        await seeder.SeedAsync(caller, default); await seeder.SeedAsync(caller, default);
        // New store/context/connections, not the same in-memory state.
        var restarted = new SqlServerFleetStore(options);
        var persisted = await restarted.ReadAsync(tenant, default);
        Assert.Equal(4, persisted.Drivers.Count); Assert.Equal(4, persisted.Vehicles.Count);
        Assert.Equal(7, persisted.Associations.Count); Assert.Equal(4, persisted.Associations.Count(a => a.Until is null)); Assert.Equal(22, persisted.Audit.Count);
        Assert.Empty((await restarted.ReadAsync(Guid.NewGuid().ToString("D"), default)).Drivers);
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.TransactAsync<int>(tenant, s =>
        { s.Drivers.Clear(); throw new InvalidOperationException("Requested test rollback"); }, default));
        Assert.Equal(4, (await restarted.ReadAsync(tenant, default)).Drivers.Count);
        // SQL filtered unique index and transaction must reject a second active pair.
        var pair = persisted.Associations.First(a => a.Until is null);
        await Assert.ThrowsAsync<FleetException>(() => restarted.TransactAsync(tenant, s =>
        { s.Associations.Add(pair with { Id = Guid.NewGuid() }); return 0; }, default));
        Assert.Equal(7, (await restarted.ReadAsync(tenant, default)).Associations.Count);
    }
}
