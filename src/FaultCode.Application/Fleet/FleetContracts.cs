using FaultCode.Domain;

namespace FaultCode.Application.Fleet;

// Validated identity and exact permission grants, built by the API, never model-bound.
public sealed record FleetCaller(string TenantId, string ActorId, string ClientId, string CorrelationId,
    bool AllDrivers, bool OwnDrivers, bool AllVehicles, bool OwnVehicles);
public sealed record DriverInput(string Identifier, string OwnerObjectId, string DisplayName, string? Email, bool EmailNotifications);
public sealed record DriverUpdate(string DisplayName, string? Email, bool EmailNotifications);
public sealed record VehicleInput(string Identifier, string System);
public sealed record FleetAudit(string TenantId, string ActorId, string ClientId, string CorrelationId,
    string Operation, Guid ResourceId, DateTimeOffset At);
public sealed record Replay(string PayloadHash, MutationResult Result);
public sealed record MutationResult(Guid Id, long Version);
public sealed record FleetPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

// Each transaction is restricted to one tenant. Never return another tenant's records.
// Implementations MUST atomically commit state/history/replay/audit or roll back all changes,
// serialize conflicting writes across processes, enforce cancellation before commit, and
// return detached snapshots. Production durability requires DEC-001 approval and adapter tests.
public interface IFleetStore
{
    Task<FleetState> ReadAsync(string tenantId, CancellationToken cancellationToken);
    Task<T> TransactAsync<T>(string tenantId, Func<FleetState, T> operation, CancellationToken cancellationToken);
}
public sealed class FleetState
{
    public List<Driver> Drivers { get; } = [];
    public List<Vehicle> Vehicles { get; } = [];
    public List<Association> Associations { get; } = [];
    public List<FleetAudit> Audit { get; } = [];
    public Dictionary<string, Replay> Replays { get; } = new(StringComparer.Ordinal);
    public FleetState Copy()
    {
        var copy = new FleetState();
        copy.Drivers.AddRange(Drivers); copy.Vehicles.AddRange(Vehicles);
        copy.Associations.AddRange(Associations); copy.Audit.AddRange(Audit);
        foreach (var pair in Replays) copy.Replays.Add(pair.Key, pair.Value);
        return copy;
    }
}
public sealed class FleetException(int status) : Exception
{
    public int Status { get; } = status;
}
