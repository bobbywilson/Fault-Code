using FaultCode.Application.Fleet;
using FaultCode.Application.Incidents;
using FaultCode.Application.Investigations;
using FaultCode.Application.Knowledge;
using FaultCode.Application.Ontology;
using FaultCode.Application.Retrieval;
using FaultCode.Domain;

namespace FaultCode.Application.Escalations;

public sealed record EngineeringReplay(string Payload, MutationResult Result);
public sealed record EngineeringAudit(string ActorId, string ClientId, string CorrelationId, string Operation, Guid Id, long Version, DateTimeOffset At);
public sealed class EngineeringState
{
    public List<EngineeringCase> Cases { get; } = [];
    public List<EngineeringEvent> Events { get; } = [];
    public List<EngineeringAudit> Audit { get; } = [];
    public Dictionary<string, EngineeringReplay> Replays { get; } = new(StringComparer.Ordinal);
    public EngineeringState Copy()
    {
        var s = new EngineeringState(); s.Cases.AddRange(Cases); s.Events.AddRange(Events); s.Audit.AddRange(Audit);
        foreach (var p in Replays) s.Replays.Add(p.Key, p.Value); return s;
    }
}
public sealed record EngineeringTransaction(EngineeringState Engineering, IncidentState Incidents, KnowledgeState Knowledge,
    OntologyState Ontology, DocumentState Documents, InvestigationState Investigations);
// MUST join the SAME authoritative unit of work as all existing stores; never sequentially copy or
// independently commit their states. Serialize tenant/case/incident/code uniqueness across processes.
// Detached consistent snapshots, atomic knowledge/history/incident/case/event/replay/audit commit,
// cancellation/failure rollback, immutable retained evidence and durable event retry by stable ID.
// Requires DEC-001/003 adapter acceptance: crash/restart/concurrent connections/retention and joined
// authoritative fleet/knowledge/document/ontology/investigation changes. No external dispatch here.
public interface IEscalationStore
{
    Task<T> Execute<T>(string tenant, Func<EngineeringTransaction, T> action, CancellationToken ct);
}
