using FaultCode.Application.Fleet;

namespace FaultCode.Application.Demo;

public sealed record DemoFleetData(IReadOnlyList<DriverInput> Drivers, IReadOnlyList<VehicleInput> Vehicles,
    IReadOnlyList<(int Driver, int Vehicle)> Associations);

// Deterministic demo factory, not a runtime production store or a real directory-user generator.
public static class DemoFleetFactory
{
    public static DemoFleetData Create()
    {
        var prior = LegacyV1();
        return prior with { Associations = [(0, 0), (1, 1), (2, 2), (3, 3)], Drivers = prior.Drivers.Select(d => d with
        { DisplayName = d.DisplayName.Replace("Synthetic ", "", StringComparison.Ordinal), Email = d.Email!.Replace("@example.invalid", "@example.com", StringComparison.Ordinal) }).ToArray() };
    }
    // Preserve original replay payloads when upgrading an already seeded database.
    internal static DemoFleetData LegacyV1() => new(
        [
            new("DRV-DEMO-001", "d1000000-0000-4000-8000-000000000001", "Synthetic Alex Morgan", "alex.morgan@example.invalid", false),
            new("DRV-DEMO-002", "d1000000-0000-4000-8000-000000000002", "Synthetic Jordan Lee", "jordan.lee@example.invalid", false),
            new("DRV-DEMO-003", "d1000000-0000-4000-8000-000000000003", "Synthetic Casey Taylor", "casey.taylor@example.invalid", false),
            new("DRV-DEMO-004", "d1000000-0000-4000-8000-000000000004", "Synthetic Riley Parker", "riley.parker@example.invalid", false)
        ],
        [
            new("SYN-SILVERADO-001", "EPS"), new("SYN-SILVERADO-002", "EPS"),
            new("SYN-SIERRA-001", "EPS"), new("SYN-SIERRA-002", "EPS")
        ],
        [(0, 0), (1, 0), (0, 1), (2, 2), (3, 2), (3, 3)]);
}

// Invoke explicitly from a trusted administrative seed runner once a durable adapter is approved.
// Reuses domain validation, transactions, audit and replay handling. Never runs on API startup.
public sealed class DemoFleetSeeder(FleetService fleet)
{
    public async Task SeedAsync(FleetCaller caller, CancellationToken ct)
    {
        if (!caller.AllDrivers || !caller.AllVehicles) throw new FleetException(403);
        var data = DemoFleetFactory.LegacyV1();
        var drivers = new List<MutationResult>();
        var vehicles = new List<MutationResult>();
        for (var i = 0; i < data.Drivers.Count; i++)
            drivers.Add(await fleet.CreateDriver(caller, data.Drivers[i], $"demo-fleet-v1-driver-{i}", ct));
        for (var i = 0; i < data.Vehicles.Count; i++)
            vehicles.Add(await fleet.CreateVehicle(caller, data.Vehicles[i], $"demo-fleet-v1-vehicle-{i}", ct));
        // Stable order and expected versions allow an interrupted run to resume with the same caller.
        for (var i = 0; i < data.Associations.Count; i++)
        {
            var pair = data.Associations[i]; var vehicle = vehicles[pair.Vehicle];
            var result = await fleet.Associate(caller, vehicle.Id, drivers[pair.Driver].Id, vehicle.Version,
                $"demo-fleet-v1-association-{i}", ct);
            vehicles[pair.Vehicle] = result;
        }
        var current = DemoFleetFactory.Create();
        for (var i = 0; i < drivers.Count; i++)
        {
            var stored = await fleet.GetDriver(caller, drivers[i].Id, ct);
            var old = data.Drivers[i]; var desired = current.Drivers[i];
            if (stored.DisplayName == old.DisplayName && stored.Email == old.Email)
                await fleet.UpdateDriver(caller, stored.Id, new(desired.DisplayName, desired.Email, stored.EmailNotifications), stored.Version, ct);
        }

        // Later approved demo layout: one assigned driver for each vehicle, retaining ended history.
        var seedDriverIds = drivers.Select(d => d.Id).ToHashSet();
        foreach (var pair in current.Associations)
        {
            var vehicleId = vehicles[pair.Vehicle].Id; var wanted = drivers[pair.Driver].Id;
            var active = (await fleet.History(caller, vehicleId, 1, 100, ct)).Items.Where(a => a.Until is null).ToArray();
            // Do not overwrite unrelated user data in an existing database.
            if (active.Any(a => !seedDriverIds.Contains(a.DriverId))) continue;
            var version = (await fleet.GetVehicle(caller, vehicleId, ct)).Vehicle.Version;
            foreach (var association in active.Where(a => a.DriverId != wanted))
                version = (await fleet.EndAssociation(caller, vehicleId, association.Id, version, ct)).Version;
            if (!active.Any(a => a.DriverId == wanted))
                await fleet.Associate(caller, vehicleId, wanted, version, $"demo-fleet-v3-association-{pair.Vehicle}", ct);
        }

    }
}
