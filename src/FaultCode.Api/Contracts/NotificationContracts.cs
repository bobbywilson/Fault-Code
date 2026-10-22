using System.Text.Json.Serialization;
using FaultCode.Domain;

namespace FaultCode.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EnqueueNotificationRequest(string SourceKind, Guid SourceId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NotificationActionRequest;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LocalNotificationPreferenceRequest(bool Enabled, string? Contact);
public sealed record LocalNotificationPreferenceResponse(Guid DriverId, bool Enabled, bool HasContact, long DriverVersion)
{
    public static LocalNotificationPreferenceResponse From(LocalNotificationPreference p) => new(p.DriverId, p.Enabled, p.Contact is not null, p.DriverVersion);
}
public sealed record NotificationResponse(Guid Id, Guid IncidentId, Guid DriverId, string SourceKind, Guid SourceId,
    Guid FaultId, long PublicationVersion, long OntologyVersion, string RemedyId, NotificationPatchResponse? Patch,
    IReadOnlyList<NotificationEvidenceResponse> Evidence, string TemplateVersion, string Message, string State, long Version,
    DateTimeOffset CreatedAt, DateTimeOffset? NextAttemptAt, DateTimeOffset? LeaseUntil, IReadOnlyList<NotificationAttemptResponse> Attempts)
{
    public static NotificationResponse From(DriverNotification n) => new(n.Id, n.IncidentId, n.DriverId, n.SourceKind, n.SourceId,
        n.FaultId, n.PublicationVersion, n.OntologyVersion, n.Remedy.Id,
        n.Remedy.Patch is { } p ? new(p.Reference, p.SoftwareVersion) : null, n.Evidence.Select(NotificationEvidenceResponse.From).ToArray(), n.TemplateVersion, n.Message,
        n.State, n.Version, n.CreatedAt, n.NextAttemptAt, n.LeaseUntil, n.Attempts.Select(NotificationAttemptResponse.From).ToArray());
}

public sealed record NotificationPatchResponse(string Reference, string SoftwareVersion);

public sealed record NotificationEvidenceResponse(Guid DocumentId, string SourceId, string SourceVersion, long PublicationVersion, string Checksum)
{
    public static NotificationEvidenceResponse From(NotificationEvidence e) => new(e.DocumentId, e.SourceId, e.SourceVersion, e.PublicationVersion, e.Checksum);
}
public sealed record NotificationAttemptResponse(Guid Id, int Number, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, string State, string? FailureCode, string? Acknowledgement)
{
    public static NotificationAttemptResponse From(NotificationAttempt a) => new(a.Id, a.Number, a.StartedAt, a.CompletedAt, a.State, a.FailureCode, a.Acknowledgement);
}
public sealed record NotificationHistoryResponse(long Version, string State, string Action, DateTimeOffset At, string ActorId, string ClientId, string CorrelationId)
{
    public static NotificationHistoryResponse From(NotificationHistory h) => new(h.Version, h.State, h.Action, h.At, h.ActorId, h.ClientId, h.CorrelationId);
}

public sealed record DriverNotificationSummaryResponse(Guid Id, Guid IncidentId, Guid DriverId, string DriverName,
    string? Email, string Message, string State, DateTimeOffset CreatedAt, DateTimeOffset? NextAttemptAt,
    int AttemptCount, string? LastFailureCode)
{
    public static DriverNotificationSummaryResponse From(FaultCode.Application.Notifications.DriverNotificationSummary item)
    {
        var n = item.Notification;
        return new(n.Id, n.IncidentId, n.DriverId, item.DriverName, item.Email, n.Message, n.State, n.CreatedAt,
            n.NextAttemptAt, n.Attempts.Length, n.Attempts.LastOrDefault()?.FailureCode);
    }
}
