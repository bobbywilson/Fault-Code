using FaultCode.Application.Incidents;
using FaultCode.Application.Investigations;

namespace FaultCode.Infrastructure;

public sealed class UnavailableInvestigationStore : IInvestigationStore
{
    public Task<T> Execute<T>(string tenant, Func<InvestigationState, Task<T>> action, CancellationToken ct) =>
        Task.FromException<T>(new IncidentException(503));
}
