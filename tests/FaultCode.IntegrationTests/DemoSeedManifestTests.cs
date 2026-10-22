using System.Text.Json;
using FaultCode.Api.Contracts;
using FaultCode.Domain;

namespace FaultCode.IntegrationTests;

public sealed class DemoSeedManifestTests
{
    [Fact]
    public async Task Fleet_factory_and_seeder_create_many_to_many_demo_records_and_replay_without_duplicates()
    {
        var store = new TestFleetStore();
        var fleet = new FaultCode.Application.Fleet.FleetService(store, TimeProvider.System);
        var seeder = new FaultCode.Application.Demo.DemoFleetSeeder(fleet);
        var caller = new FaultCode.Application.Fleet.FleetCaller(EntraAuthenticationTests.Tenant,
            EntraAuthenticationTests.Actor, EntraAuthenticationTests.Client, "demo-seed", true, false, true, false);
        await seeder.SeedAsync(caller, default);
        var before = await store.ReadAsync(caller.TenantId, default);
        await seeder.SeedAsync(caller, default);
        var after = await store.ReadAsync(caller.TenantId, default);
        Assert.Equal(4, after.Drivers.Count); Assert.Equal(4, after.Vehicles.Count);
        Assert.Equal(7, after.Associations.Count); Assert.Equal(4, after.Associations.Count(a => a.Until is null));
        Assert.All(after.Vehicles, v => Assert.Single(after.Associations, a => a.VehicleId == v.Id && a.Until is null)); Assert.Equal(before.Audit.Count, after.Audit.Count);
        Assert.All(after.Drivers, d => { Assert.EndsWith("@example.com", d.Email); Assert.DoesNotContain("Synthetic", d.DisplayName); Assert.False(d.EmailNotifications); });
        var vehicles = await fleet.Vehicles(caller, 1, 20, null, default);
        Assert.Equal(4, vehicles.TotalCount);
        Assert.Equal(2, vehicles.Items.Count(v => v.Identifier.Contains("SILVERADO")));
        Assert.Equal(2, vehicles.Items.Count(v => v.Identifier.Contains("SIERRA")));
        var alex = after.Drivers.Single(d => d.Identifier == "DRV-DEMO-001");
        var jordan = after.Drivers.Single(d => d.Identifier == "DRV-DEMO-002");
        var own = caller with { ActorId = jordan.OwnerObjectId, AllDrivers = false, AllVehicles = false, OwnDrivers = true, OwnVehicles = true };
        var ownVehicles = await fleet.Vehicles(own, 1, 20, null, default);
        Assert.Single(ownVehicles.Items);
        var read = await fleet.GetVehicle(own, ownVehicles.Items[0].Id, default);
        Assert.Equal(jordan.Id, read.Owner.Id);
        Assert.DoesNotContain((await fleet.Drivers(own, 1, 20, null, default)).Items, d => d.Id == alex.Id);
        Assert.Empty((await fleet.Vehicles(own with { ActorId = "d1000000-0000-4000-8000-000000000099" }, 1, 20, null, default)).Items);
        Assert.Empty((await store.ReadAsync("d2000000-0000-4000-8000-000000000001", default)).Vehicles);
        await Assert.ThrowsAsync<FaultCode.Application.Fleet.FleetException>(() => seeder.SeedAsync(own, default));
        var unavailable = new FaultCode.Application.Demo.DemoFleetSeeder(new(
            new FaultCode.Infrastructure.UnavailableFleetStore(), TimeProvider.System));
        var blocked = await Assert.ThrowsAsync<FaultCode.Application.Fleet.FleetException>(() => unavailable.SeedAsync(caller, default));
        Assert.Equal(503, blocked.Status);

    }

    [Fact]
    public void Prepared_seed_is_explicitly_synthetic_and_passes_existing_domain_validation()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Seeds", "steering-faults.json")));
        var root = json.RootElement;
        Assert.True(root.GetProperty("synthetic").GetBoolean());
        Assert.Equal("prepared-not-loaded", root.GetProperty("status").GetString());
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var ontology = JsonSerializer.Deserialize<OntologyContentRequest>(root.GetProperty("ontology"), options)!.ToDomain();
        OntologyRules.Validate(ontology);
        var codes = new HashSet<string>();
        foreach (var fault in root.GetProperty("faults").EnumerateArray())
        {
            var system = fault.GetProperty("system").GetString()!;
            var code = fault.GetProperty("code").GetString()!;
            Assert.Equal(code, FaultRules.Code(code, system));
            Assert.True(codes.Add(code));
            var content = JsonSerializer.Deserialize<FaultContentRequest>(fault.GetProperty("content"), options)!.ToDomain();
            FaultRules.Validate(content);
            Assert.Contains(ontology.Concepts, c => c.Id == content.ClassificationId && c.System == system && c.Kind == "classification");
            Assert.All(content.Remedies, remedy => Assert.False(remedy.Approved));
            Assert.Contains("not a verified manufacturer or OEM", content.Description);
        }
        Assert.Equal(9, codes.Count);
    }
}
