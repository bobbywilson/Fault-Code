using FaultCode.Application.Escalations;
using FaultCode.Application.Fleet;
using FaultCode.Domain;

namespace FaultCode.Application.Notifications;

public sealed record NotificationReplay(string Payload, MutationResult Result);
public sealed record NotificationAudit(string ActorId, string ClientId, string CorrelationId, string Operation, Guid Id, long Version, DateTimeOffset At);
public sealed class NotificationState
{
    public List<DriverNotification> Messages { get; } = [];
    public List<LocalNotificationPreference> Preferences { get; } = [];
    public List<NotificationAudit> Audit { get; } = [];
    public Dictionary<string, NotificationReplay> Replays { get; } = new(StringComparer.Ordinal);
    public NotificationState Copy()
    {
        var s = new NotificationState(); s.Messages.AddRange(Messages); s.Preferences.AddRange(Preferences); s.Audit.AddRange(Audit);
        foreach (var p in Replays) s.Replays.Add(p.Key, p.Value); return s;
    }
}
public sealed record NotificationTransaction(NotificationState Notifications, FleetState Fleet, EngineeringTransaction Context);
// One authoritative transaction joins ALL existing fleet/incident/engineering/knowledge/ontology/document/
// investigation adapters, including their writes; not sequential independent copies or commits. Enforce tenant
// source uniqueness, one active message per incident, version/replay/lease fencing across processes. Detached
// snapshots, atomic history/audit/outbox/replay/incident/preferences commit; rollback on exception/cancellation.
// No external I/O in callbacks. DEC-001/003 durable acceptance: restart/crash/retention/multiple connections.
public interface INotificationStore
{
    Task<T> Execute<T>(string tenant, Func<NotificationTransaction, T> action, CancellationToken ct);
}
public sealed record LocalDelivery(Guid NotificationId, string SyntheticContact, string Message);
public sealed record DeliveryAcknowledgement(string Receipt);
// Future providers MUST document stable NotificationId deduplication, uncertain-acceptance reconciliation,
// bounded acknowledgement and deadlines. No model access, credentials or external adapter selected here.
public interface INotificationDelivery
{
    Task<DeliveryAcknowledgement> Send(LocalDelivery message, CancellationToken ct);
}
