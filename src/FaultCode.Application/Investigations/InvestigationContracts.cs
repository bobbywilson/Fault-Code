using System.Collections.Immutable;
using FaultCode.Application.Knowledge;
using FaultCode.Application.Retrieval;
using FaultCode.Domain;

namespace FaultCode.Application.Investigations;

public sealed record InvestigationRule(long PublicationVersion, string ApprovedBy, DateTimeOffset ApprovedAt,
    OntologyInput Input, OntologyOutcome Outcome);
public sealed record InvestigationModel(string Model, string InstructionsVersion, int Attempts, long InputTokens,
    long OutputTokens, int ReservedTokens, bool ReviewRequired);
public sealed record InvestigationProposal(string Kind, string Status);
public sealed record InvestigationResult(Guid Id, Guid IncidentId, long IncidentVersion, DateTimeOffset CreatedAt,
    string CorrelationId, string Classification, ApprovedFault? Definition, InvestigationRule? Rule,
    ImmutableArray<EvidenceCitation> Citations, FaultRemedy? Remedy, ImmutableArray<string> Uncertainties,
    InvestigationProposal ProposedAction, InvestigationModel Model, string PolicyVersion);
public sealed record InvestigationEntry(string TenantId, string OwnerId, string ActorId, string ClientId, InvestigationResult Result);
public sealed record InvestigationReplay(long ExpectedVersion, Guid ResultId);
public sealed record InvestigationAudit(string ActorId, string ClientId, string CorrelationId, Guid ResultId, DateTimeOffset At);
// MUST coordinate tenant serialization with incident/knowledge/ontology/document authoritative reads
// throughout async callback and commit, across processes. Atomic detached copy-on-write result/replay/audit
// with rollback on failure/cancellation, immutable retained results and crash/restart/retention acceptance.
// Reentrant storage adapters must use one shared coordinated transaction rather than nested independent locks.
// No external action dispatch in callbacks. Production remains503 until DEC-001/003.
public interface IInvestigationStore
{
    Task<T> Execute<T>(string tenant, Func<InvestigationState, Task<T>> action, CancellationToken ct);
}
public sealed class InvestigationState
{
    public List<InvestigationEntry> Results { get; } = [];
    public List<InvestigationAudit> Audit { get; } = [];
    public Dictionary<string, InvestigationReplay> Replays { get; } = new(StringComparer.Ordinal);
    public InvestigationState Copy()
    {
        var s = new InvestigationState(); s.Results.AddRange(Results); s.Audit.AddRange(Audit);
        foreach (var p in Replays) s.Replays.Add(p.Key, p.Value);
        return s;
    }
}
