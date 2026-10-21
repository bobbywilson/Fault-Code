using FaultCode.Application.Knowledge;
using FaultCode.Domain;
namespace FaultCode.Infrastructure;

public sealed class UnavailableKnowledgeStore : IKnowledgeStore
{
    public Task<KnowledgeState> ReadAsync(string tenantId, CancellationToken ct) => throw new KnowledgeException(503);
    public Task<T> TransactAsync<T>(string tenantId, Func<KnowledgeState, T> operation, CancellationToken ct) => throw new KnowledgeException(503);
}
public sealed class UnavailableFaultTaxonomy : IFaultTaxonomy
{
    public Task<bool> ValidateAsync(string tenantId, string system, FaultContent content, CancellationToken ct) => throw new KnowledgeException(503);
}
