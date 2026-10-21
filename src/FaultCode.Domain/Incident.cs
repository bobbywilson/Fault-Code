using System.Collections.Immutable;

namespace FaultCode.Domain;

public sealed record IncidentFleet(Guid VehicleId, string VehicleIdentifier, long VehicleVersion,
    Guid DriverId, string DriverIdentifier, long DriverVersion, string OwnerObjectId, Association Association);
public sealed record IncidentKnowledge(Guid DefinitionId, long PublicationVersion, FaultContent Content, string ApprovedBy, DateTimeOffset ApprovedAt);
public sealed record IncidentHistory(long Version, string Status, string Action, string ActorId, string ClientId, string CorrelationId,
    DateTimeOffset At, string? FailureCode);
public sealed record IncidentWork(Guid Id, string Kind, string State, int Attempts, string? FailureCode);
public sealed record Incident(Guid Id, string TenantId, string SourceClientId, string SourceEventId, string Code, string System,
    string Model, string SoftwareVersion, DateTimeOffset OccurredAt, DateTimeOffset ReceivedAt, string IntakeCorrelationId,
    IncidentFleet Fleet, IncidentKnowledge? Knowledge, string Status, long Version,
    ImmutableArray<IncidentHistory> History, ImmutableArray<IncidentWork> Work) : IAggregateRoot;

public static class IncidentRules
{
    public static bool Token(string? value) => value is { Length: >= 1 and <= 100 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    public static bool Status(string? value) => value is "Received" or "Investigating" or "Escalated" or "Resolved" or "NotificationPending" or "Notified";
    public static bool CanTransition(string from, string to) => (from, to) is
        ("Received", "Investigating") or ("Investigating", "Escalated") or ("Investigating", "Resolved")
        or ("Escalated", "Investigating") or ("Escalated", "Resolved")
        or ("Resolved", "NotificationPending") or ("NotificationPending", "Notified");
    public static bool Occurrence(DateTimeOffset at, DateTimeOffset now) => at.Offset == TimeSpan.Zero
        && at >= now.AddDays(-7) && at <= now;
    public static bool Failure(string? code) => code is "dispatch-unavailable" or "processing-unavailable" or "dependency-unavailable";
}
