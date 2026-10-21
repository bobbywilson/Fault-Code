using FaultCode.Application.Incidents;

namespace FaultCode.Infrastructure;

// No production memory fallback. Authoritative fleet/knowledge integration and durability await DEC-001.
public sealed class UnavailableIncidentStore : IIncidentStore
{
    public Task<IncidentState> ReadAsync(string tenantId, CancellationToken ct) => throw new IncidentException(503);
    public Task<T> TransactAsync<T>(string tenantId, Func<IncidentTransaction, T> operation, CancellationToken ct) => throw new IncidentException(503);
}
