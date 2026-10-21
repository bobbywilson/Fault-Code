using FaultCode.Domain;
using FaultCode.Application.Fleet;

namespace FaultCode.Application.Knowledge;

public sealed record KnowledgeCaller(string TenantId, string ActorId, string ClientId, string CorrelationId);
public sealed record FaultCreate(string Code, string System, FaultContent Content);
public sealed record KnowledgeAudit(string TenantId, string ActorId, string ClientId, string CorrelationId, string Operation, Guid Id, long Version, DateTimeOffset At);
public sealed record KnowledgeReplay(string Hash, MutationResult Result);
public sealed record ApprovedFault(Guid Id, string Code, string System, long Version, FaultContent Content, string ApprovedBy, DateTimeOffset ApprovedAt);
// Must validate the exact approved taxonomy version, system and each allowed remedy kind.
// Invalid/unapproved links return false; service unavailability throws KnowledgeException(503).
public interface IFaultTaxonomy
{
    Task<bool> ValidateAsync(string tenantId, string system, FaultContent content, CancellationToken ct);
}
// One detached tenant snapshot per read. Transactions MUST atomically commit records, immutable
// revisions/publications, replay and audit, or roll back on exception/cancellation. Enforce uniqueness
// and versions across processes. Approved durable adapter and recovery tests await DEC-001.
public interface IKnowledgeStore
{
    Task<KnowledgeState> ReadAsync(string tenantId, CancellationToken ct);
    Task<T> TransactAsync<T>(string tenantId, Func<KnowledgeState, T> operation, CancellationToken ct);
}
public sealed class KnowledgeState
{
    public List<FaultDefinition> Faults { get; } = [];
    public List<KnowledgeAudit> Audit { get; } = [];
    public Dictionary<string, KnowledgeReplay> Replays { get; } = new(StringComparer.Ordinal);
    public KnowledgeState Copy()
    {
        var copy = new KnowledgeState(); copy.Faults.AddRange(Faults); copy.Audit.AddRange(Audit);
        foreach (var pair in Replays) copy.Replays.Add(pair.Key, pair.Value);
        return copy;
    }
}
public sealed class KnowledgeException(int status, string? failureCode = null) : Exception
{
    public int Status { get; } = status;
    public string? FailureCode { get; } = failureCode;
}
