using FaultCode.Application.Escalations;
using FaultCode.Application.Incidents;

namespace FaultCode.Infrastructure;

public sealed class UnavailableEscalationStore : IEscalationStore
{
    public Task<T> Execute<T>(string tenant, Func<EngineeringTransaction, T> action, CancellationToken ct) => throw new IncidentException(503);
}
