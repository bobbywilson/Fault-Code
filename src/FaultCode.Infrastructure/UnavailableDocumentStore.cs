using FaultCode.Application.Knowledge;
using FaultCode.Application.Retrieval;
namespace FaultCode.Infrastructure;

public sealed class UnavailableDocumentStore : IDocumentStore
{
    public Task<T> Execute<T>(string tenant, Func<DocumentState, Task<T>> operation, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); throw new KnowledgeException(503, "document-storage-unavailable"); }
}
