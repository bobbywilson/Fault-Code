using FaultCode.Application.Fleet;

namespace FaultCode.Infrastructure;

// Deliberately no process-local fallback: DEC-001 durable adapter is unapproved.
public sealed class UnavailableFleetStore : IFleetStore
{
    public Task<FleetState> ReadAsync(string tenantId, CancellationToken cancellationToken) => throw new FleetException(503);
    public Task<T> TransactAsync<T>(string tenantId, Func<FleetState, T> operation, CancellationToken cancellationToken) => throw new FleetException(503);
}
