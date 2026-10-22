using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FaultCode.Domain;

namespace FaultCode.Application.Fleet;

public sealed class FleetService(IFleetStore store, TimeProvider clock)
{
    private static void ValidateCaller(FleetCaller caller)
    {
        if (!FleetRules.CanonicalId(caller.TenantId) || !FleetRules.CanonicalId(caller.ActorId)
            || !FleetRules.CanonicalId(caller.ClientId) || string.IsNullOrWhiteSpace(caller.CorrelationId)) throw new FleetException(403);
    }
    private static Driver Driver(FleetState state, FleetCaller caller, Guid id) =>
        state.Drivers.SingleOrDefault(d => d.Id == id && d.TenantId == caller.TenantId) ?? throw new FleetException(404);
    private static Vehicle Vehicle(FleetState state, FleetCaller caller, Guid id) =>
        state.Vehicles.SingleOrDefault(v => v.Id == id && v.TenantId == caller.TenantId) ?? throw new FleetException(404);
    public static bool CanRead(FleetCaller caller, Driver driver, bool vehicle = false) =>
        driver.TenantId == caller.TenantId && FleetRules.CanonicalId(driver.OwnerObjectId)
        && ((vehicle ? caller.AllVehicles : caller.AllDrivers)
            || (vehicle ? caller.OwnVehicles : caller.OwnDrivers) && driver.OwnerObjectId == caller.ActorId);
    public static Driver? VehicleOwner(FleetState state, FleetCaller caller, Vehicle vehicle)
    {
        if (vehicle.TenantId != caller.TenantId) return null;
        // Choose a mapping the caller may read, not an arbitrary first driver. Multiple active drivers are valid.
        return state.Associations.Where(a => a.VehicleId == vehicle.Id && a.Until is null)
            .Select(a => state.Drivers.SingleOrDefault(d => d.Id == a.DriverId && d.TenantId == caller.TenantId))
            .FirstOrDefault(d => d is not null && CanRead(caller, d, true));
    }
    private async Task<FleetState> Read(FleetCaller caller, CancellationToken ct)
    { ValidateCaller(caller); return await store.ReadAsync(caller.TenantId, ct); }
    public async Task<Driver> GetDriver(FleetCaller caller, Guid id, CancellationToken ct)
    {
        var item = Driver(await Read(caller, ct), caller, id);
        return CanRead(caller, item) ? item : throw new FleetException(404);
    }
    public async Task<(Vehicle Vehicle, Driver? Owner)> GetVehicle(FleetCaller caller, Guid id, CancellationToken ct)
    {
        var state = await Read(caller, ct); var item = Vehicle(state, caller, id);
        var owner = VehicleOwner(state, caller, item);
        return caller.AllVehicles || owner is not null && CanRead(caller, owner, true) ? (item, owner) : throw new FleetException(404);
    }
    public async Task<FleetPage<Driver>> Drivers(FleetCaller caller, int page, int size, bool? active, CancellationToken ct) =>
        Page((await Read(caller, ct)).Drivers.Where(d => CanRead(caller, d) && (active is null || d.Active == active)).OrderBy(d => d.Id), page, size);
    public async Task<FleetPage<Vehicle>> Vehicles(FleetCaller caller, int page, int size, bool? active, CancellationToken ct)
    {
        var state = await Read(caller, ct);
        return Page(state.Vehicles.Where(v => v.TenantId == caller.TenantId && (caller.AllVehicles || VehicleOwner(state, caller, v) is { } owner && CanRead(caller, owner, true))
            && (active is null || v.Active == active)).OrderBy(v => v.Id), page, size);
    }
    private static FleetPage<T> Page<T>(IEnumerable<T> source, int page, int size)
    {
        if (page < 1 || size is < 1 or > 100) throw new FleetException(400);
        var all = source.ToArray(); var offset = ((long)page - 1) * size;
        return new(offset >= all.Length ? [] : all.Skip((int)offset).Take(size).ToArray(), page, size, all.Length);
    }
    public async Task<FleetPage<Association>> History(FleetCaller caller, Guid vehicleId, int page, int size, CancellationToken ct)
    {
        var state = await Read(caller, ct); var vehicle = Vehicle(state, caller, vehicleId);
        // Own readers see only intervals belonging to their trusted driver mapping, including ended intervals.
        var allowed = state.Associations.Where(a => a.VehicleId == vehicle.Id
            && state.Drivers.Any(d => d.Id == a.DriverId && CanRead(caller, d, true))).OrderBy(a => a.From).ThenBy(a => a.Id);
        return Page(allowed, page, size);
    }
    public Task<MutationResult> CreateDriver(FleetCaller caller, DriverInput input, string key, CancellationToken ct)
    {
        var identifier = Normalize(input.Identifier, "DRV-");
        ValidateDriver(input.DisplayName, input.Email, input.EmailNotifications);
        if (!FleetRules.CanonicalId(input.OwnerObjectId)) throw new FleetException(400);
        return Mutate(caller, "drivers.create", key, input with { Identifier = identifier, DisplayName = input.DisplayName.Trim(), Email = input.Email?.Trim().ToLowerInvariant() }, s =>
        {
            if (s.Drivers.Any(d => d.TenantId == caller.TenantId && (d.Identifier == identifier || d.OwnerObjectId == input.OwnerObjectId))) throw new FleetException(409);
            var d = new Driver(Guid.NewGuid(), caller.TenantId, identifier, input.OwnerObjectId, input.DisplayName.Trim(), input.Email?.Trim().ToLowerInvariant(), input.EmailNotifications, true, 1);
            s.Drivers.Add(d); return new(d.Id, d.Version);
        }, ct);
    }
    public Task<MutationResult> CreateVehicle(FleetCaller caller, VehicleInput input, string key, CancellationToken ct)
    {
        var identifier = Normalize(input.Identifier, "SYN-"); ValidateSystem(input.System);
        return Mutate(caller, "vehicles.create", key, input with { Identifier = identifier }, s =>
        {
            if (s.Vehicles.Any(v => v.TenantId == caller.TenantId && v.Identifier == identifier)) throw new FleetException(409);
            var v = new Vehicle(Guid.NewGuid(), caller.TenantId, identifier, input.System, true, 1);
            s.Vehicles.Add(v); return new(v.Id, v.Version);
        }, ct);
    }
    public Task<MutationResult> UpdateDriver(FleetCaller c, Guid id, DriverUpdate input, long version, CancellationToken ct)
    {
        ValidateDriver(input.DisplayName, input.Email, input.EmailNotifications);
        return Mutate(c, "drivers.update", null, input, s =>
        {
            var d = Driver(s, c, id); Check(d.Version, version); if (!d.Active) throw new FleetException(409);
            s.Drivers[s.Drivers.IndexOf(d)] = d with { DisplayName = input.DisplayName.Trim(), Email = input.Email?.Trim().ToLowerInvariant(), EmailNotifications = input.EmailNotifications, Version = d.Version + 1 };
            return new(id, d.Version + 1);
        }, ct);
    }
    public Task<MutationResult> UpdateVehicle(FleetCaller c, Guid id, VehicleInput input, long version, CancellationToken ct)
    {
        var identifier = Normalize(input.Identifier, "SYN-"); ValidateSystem(input.System);
        return Mutate(c, "vehicles.update", null, input, s =>
        {
            var v = Vehicle(s, c, id); Check(v.Version, version); if (!v.Active) throw new FleetException(409);
            if (s.Vehicles.Any(other => other.TenantId == c.TenantId && other.Id != id && other.Identifier == identifier)) throw new FleetException(409);
            s.Vehicles[s.Vehicles.IndexOf(v)] = v with { Identifier = identifier, System = input.System, Version = v.Version + 1 };
            return new(id, v.Version + 1);
        }, ct);
    }
    public Task<MutationResult> Deactivate(FleetCaller c, Guid id, bool driver, long version, CancellationToken ct) =>
        Mutate(c, driver ? "drivers.deactivate" : "vehicles.deactivate", null, id, s =>
        {
            if (driver)
            {
                var d = Driver(s, c, id); Check(d.Version, version); if (!d.Active) throw new FleetException(409);
                s.Drivers[s.Drivers.IndexOf(d)] = d with { Active = false, Version = d.Version + 1 };
                foreach (var a in s.Associations.Where(a => a.DriverId == id && a.Until is null).ToArray())
                { Close(s, a); BumpVehicle(s, c, a.VehicleId); }
            }
            else
            {
                var v = Vehicle(s, c, id); Check(v.Version, version); if (!v.Active) throw new FleetException(409);
                s.Vehicles[s.Vehicles.IndexOf(v)] = v with { Active = false, Version = v.Version + 1 };
                foreach (var a in s.Associations.Where(a => a.VehicleId == id && a.Until is null).ToArray()) Close(s, a);
            }
            return new(id, version + 1);
        }, ct);
    public Task<MutationResult> Associate(FleetCaller c, Guid vehicleId, Guid driverId, long version, string key, CancellationToken ct) =>
        Mutate(c, "vehicles.associate/" + vehicleId, key, new { vehicleId, driverId, version }, s =>
        {
            var v = Vehicle(s, c, vehicleId); var d = Driver(s, c, driverId); Check(v.Version, version);
            if (!FleetRules.CanAssociate(v, d, s.Associations)) throw new FleetException(409);
            s.Associations.Add(new(Guid.NewGuid(), vehicleId, driverId, clock.GetUtcNow(), null)); BumpVehicle(s, c, vehicleId);
            return new(vehicleId, version + 1);
        }, ct);
    public Task<MutationResult> EndAssociation(FleetCaller c, Guid vehicleId, Guid associationId, long version, CancellationToken ct) =>
        Mutate(c, "vehicles.end-association", null, associationId, s =>
        {
            var v = Vehicle(s, c, vehicleId); Check(v.Version, version);
            var a = s.Associations.SingleOrDefault(a => a.Id == associationId && a.VehicleId == vehicleId) ?? throw new FleetException(404);
            if (a.Until is not null) throw new FleetException(409);
            Close(s, a); BumpVehicle(s, c, vehicleId); return new(vehicleId, version + 1);
        }, ct);
    private void Close(FleetState s, Association a) => s.Associations[s.Associations.IndexOf(a)] = a with { Until = clock.GetUtcNow() };
    private static void BumpVehicle(FleetState s, FleetCaller c, Guid id)
    { var v = Vehicle(s, c, id); s.Vehicles[s.Vehicles.IndexOf(v)] = v with { Version = v.Version + 1 }; }
    private Task<MutationResult> Mutate(FleetCaller c, string op, string? key, object payload, Func<FleetState, MutationResult> operation, CancellationToken ct)
    {
        ValidateCaller(c);
        if (key is not null && (key.Length is < 1 or > 100 || key.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch is not ('-' or '_')))) throw new FleetException(400);
        var replayKey = JsonSerializer.Serialize(new[] { c.TenantId, c.ActorId, c.ClientId, op, key });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
        return store.TransactAsync(c.TenantId, s =>
        {
            if (key is not null && s.Replays.TryGetValue(replayKey, out var replay))
                return replay.PayloadHash == hash ? replay.Result : throw new FleetException(409);
            var result = operation(s);
            s.Audit.Add(new(c.TenantId, c.ActorId, c.ClientId, c.CorrelationId, op, result.Id, clock.GetUtcNow()));
            if (key is not null) s.Replays.Add(replayKey, new(hash, result));
            return result;
        }, ct);
    }
    private static void Check(long actual, long expected) { if (expected < 1) throw new FleetException(400); if (actual != expected) throw new FleetException(409); }
    private static string Normalize(string? value, string prefix)
    { try { return FleetRules.Identifier(value, prefix); } catch (ArgumentException) { throw new FleetException(400); } }
    private static void ValidateSystem(string? system) { if (system is not ("EPS" or "steer-by-wire" or "steering-ADAS")) throw new FleetException(400); }
    private static void ValidateDriver(string? name, string? email, bool notifications)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || name.Any(char.IsControl)) throw new FleetException(400);
        if (notifications && email is null) throw new FleetException(400);
        if (email is not null && (email.Length > 150 || !System.Text.RegularExpressions.Regex.IsMatch(email.Trim(), "^[a-zA-Z0-9._+-]+@example\\.(?:invalid|com)$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))) throw new FleetException(400);
    }
}
