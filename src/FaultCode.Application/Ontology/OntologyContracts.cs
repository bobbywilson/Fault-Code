using FaultCode.Application.Knowledge;
using FaultCode.Domain;

namespace FaultCode.Application.Ontology;

public sealed record OntologyAudit(string ActorId, string ClientId, string CorrelationId, string Operation, Guid Id, long Version, DateTimeOffset At);
public sealed record OntologyMutation(Guid Id, long Version);
public sealed record OntologyReplay(string Hash, OntologyMutation Result);
// A store is scoped to the supplied validated tenant. Reads MUST be detached. Transactions MUST
// atomically persist catalog/revisions/publications/evaluations/replay/audit or roll back on exceptions
// and cancellation, with cross-process version/uniqueness enforcement. Published versions are immutable
// and never removed/reassigned. Durable restart/crash/retention acceptance awaits DEC-001.
public interface IOntologyStore
{
    Task<OntologyState> ReadAsync(string tenantId, CancellationToken ct);
    Task<T> TransactAsync<T>(string tenantId, Func<OntologyState, T> operation, CancellationToken ct);
}
public sealed class OntologyState
{
    public OntologyCatalog? Catalog { get; set; }
    public List<OntologyEvaluation> Evaluations { get; } = [];
    public List<OntologyAudit> Audit { get; } = [];
    public Dictionary<string, OntologyReplay> Replays { get; } = new(StringComparer.Ordinal);
    public OntologyState Copy()
    {
        var copy = new OntologyState { Catalog = Catalog }; copy.Evaluations.AddRange(Evaluations); copy.Audit.AddRange(Audit);
        foreach (var pair in Replays) copy.Replays.Add(pair.Key, pair.Value);
        return copy;
    }
}
// All returned domain records are immutable. Store tenant isolation is mandatory, including audit/replay.
public sealed class ApprovedOntologyTaxonomy(IOntologyStore store) : IFaultTaxonomy
{
    public async Task<bool> ValidateAsync(string tenantId, string system, FaultContent content, CancellationToken ct)
    {
        var state = await store.ReadAsync(tenantId, ct);
        var publication = state.Catalog?.Publications.SingleOrDefault(p => p.Version == content.ClassificationVersion);
        var concept = publication?.Content.Concepts.SingleOrDefault(c => c.Id == content.ClassificationId && c.Kind == "classification" && c.System == system);
        return concept is not null && content.Remedies.All(r => concept.AllowedRemedyKinds.Contains(r.Kind, StringComparer.Ordinal));
    }
}
