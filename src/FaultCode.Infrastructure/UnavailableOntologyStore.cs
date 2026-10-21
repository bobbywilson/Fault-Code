using FaultCode.Application.Knowledge;
using FaultCode.Application.Ontology;

namespace FaultCode.Infrastructure;

public sealed class UnavailableOntologyStore : IOntologyStore
{
    public Task<OntologyState> ReadAsync(string tenantId, CancellationToken ct) => throw new KnowledgeException(503);
    public Task<T> TransactAsync<T>(string tenantId, Func<OntologyState, T> operation, CancellationToken ct) => throw new KnowledgeException(503);
}
