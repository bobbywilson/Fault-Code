using FaultCode.Application.Knowledge;
namespace FaultCode.Infrastructure;

public sealed class UnavailableKnowledgeStore : IKnowledgeStore
{
    public Task<KnowledgeState> ReadAsync(string tenantId, CancellationToken ct) => throw new KnowledgeException(503);
    public Task<T> TransactAsync<T>(string tenantId, Func<KnowledgeState, T> operation, CancellationToken ct) => throw new KnowledgeException(503);
}
