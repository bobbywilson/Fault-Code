using FaultCode.Application.Fleet;
using FaultCode.Domain;

namespace FaultCode.Application.Incidents;

public sealed record IncidentInput(Guid VehicleId, Guid DriverId, string SourceEventId, string Code, string System,
    string Model, string SoftwareVersion, DateTimeOffset OccurredAt);
public sealed record IncidentReplay(string Hash, MutationResult Result);
public sealed record IncidentAudit(string TenantId, string ActorId, string ClientId, string CorrelationId, string Operation,
    Guid Id, long Version, DateTimeOffset At);
// All reads MUST be detached tenant snapshots. TransactAsync MUST serialize source uniqueness,
// versions, replay, incidents/history/audit and pending work in one atomic durable unit of work.
// Context() MUST read authoritative fleet/knowledge within that same transaction/lock (including
// concurrent fleet deactivation and publication). Unavailable context throws503; never return fake empty data.
// On exception/cancellation roll back everything. Cross-process/restart/crash/retention acceptance
// requires approved DEC-001 adapters integrating the FAU-10/11 stores. No external dispatch in callbacks.
public interface IIncidentStore
{
    Task<IncidentState> ReadAsync(string tenantId, CancellationToken ct);
    Task<T> TransactAsync<T>(string tenantId, Func<IncidentTransaction, T> operation, CancellationToken ct);
}
public sealed record IncidentContext(IReadOnlyList<Vehicle> Vehicles, IReadOnlyList<Driver> Drivers,
    IReadOnlyList<Association> Associations, IReadOnlyList<FaultDefinition> Faults);
public sealed record IncidentTransaction(IncidentState State, Func<IncidentContext> Context);
public sealed class IncidentState
{
    public List<Incident> Incidents { get; } = [];
    public List<IncidentAudit> Audit { get; } = [];
    public Dictionary<string, IncidentReplay> Replays { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, IncidentReplay> Sources { get; } = new(StringComparer.Ordinal);
    public IncidentState Copy()
    {
        var copy = new IncidentState(); copy.Incidents.AddRange(Incidents); copy.Audit.AddRange(Audit);
        foreach (var pair in Replays) copy.Replays.Add(pair.Key, pair.Value);
        foreach (var pair in Sources) copy.Sources.Add(pair.Key, pair.Value);
        return copy;
    }
}
public sealed class IncidentException(int status) : Exception
{
    public int Status { get; } = status;
}
