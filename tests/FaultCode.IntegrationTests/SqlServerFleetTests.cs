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
