using System.Collections.Immutable;
using FaultCode.Domain;
using FaultCode.Application.Fleet;
using FaultCode.Application.Knowledge;
namespace FaultCode.Application.Retrieval;

public sealed record DocumentCreate(string SourceId, DocumentContent Content);
public sealed record EvidenceQuery(string Text, string System, string Model, string SoftwareVersion, int Limit = 5);
public sealed record EvidenceCitation(Guid ChunkId, Guid DocumentId, string SourceId, string SourceVersion, long PublicationVersion,
    int Page, int Offset, string Checksum, string DocumentChecksum, string Text, double Score, string ApprovedBy, DateTimeOffset ApprovedAt);
public sealed record EvidenceResult(string Status, string EvidencePolicy, ImmutableArray<EvidenceCitation> Citations);
public sealed record IndexProfile(string Model, int Dimensions)
{
    public string Signature => DocumentRules.Hash($"{Model}:{Dimensions}:{DocumentRules.Algorithm}");
    public void Validate() { if (!FaultRules.Text(Model, 100) || Model.Any(x => !char.IsAsciiLetterOrDigit(x) && x is not ('-' or '_' or '.')) || Dimensions is < 1 or > 4096) throw new KnowledgeException(503, "embedding-profile-invalid"); }
}
public sealed record VectorPoint(Guid Id, ImmutableArray<float> Vector, string RevisionKey, string System, string Model,
    string SoftwareVersion, string Access, string Publisher, Guid DocumentId);
public sealed record VectorHit(Guid Id, double Score);
public interface IEmbeddingProvider
{
    IndexProfile Profile { get; }
    Task<ImmutableArray<float>> Embed(string text, CancellationToken ct);
}
public interface IDocumentIndex
{
    Task Ensure(IndexProfile profile, CancellationToken ct);
    Task Upsert(string tenant, IndexProfile profile, ImmutableArray<VectorPoint> points, CancellationToken ct);
    Task Delete(string tenant, IndexProfile profile, Guid? documentId, CancellationToken ct);
    Task<ImmutableArray<VectorHit>> Search(string tenant, string actor, IndexProfile profile, EvidenceQuery query,
        ImmutableArray<string> allowedRevisions, ImmutableArray<float> vector, CancellationToken ct);
}
// MUST serialize tenant callbacks, including asynchronous index/search work, across processes.
// Detached copy-on-write snapshot; atomically commit originals/history/approvals/tombstones/index status/replay/audit,
// or roll back on failure/cancellation. Immutable retained originals and replay need durable approved storage
// under DEC-001/003 with restart/crash/concurrent-connection tests. No production memory fallback.
public interface IDocumentStore
{
    Task<T> Execute<T>(string tenant, Func<DocumentState, Task<T>> operation, CancellationToken ct);
}
public sealed class DocumentState
{
    public List<TechnicalDocument> Documents { get; } = [];
    public List<KnowledgeAudit> Audit { get; } = [];
    public Dictionary<string, KnowledgeReplay> Replays { get; } = new(StringComparer.Ordinal);
    public DocumentState Copy()
    {
        var s = new DocumentState(); s.Documents.AddRange(Documents); s.Audit.AddRange(Audit);
        foreach (var pair in Replays) s.Replays.Add(pair.Key, pair.Value); return s;
    }
}
public static class VectorRules
{
    public static void Validate(ImmutableArray<float> vector, IndexProfile profile)
    {
        profile.Validate();
        if (vector.IsDefault || vector.Length != profile.Dimensions || vector.Any(x => !float.IsFinite(x))
            || !vector.Any(x => x != 0)) throw new KnowledgeException(503, "embedding-response-invalid");
    }
}
