using System.Collections.Immutable;

namespace FaultCode.Domain;

public sealed record LocalNotificationPreference(Guid DriverId, bool Enabled, string? Contact, long DriverVersion);
public sealed record NotificationEvidence(Guid DocumentId, string SourceId, string SourceVersion, long PublicationVersion, string Checksum);
public sealed record NotificationAttempt(Guid Id, int Number, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, string State, string? FailureCode, string? Acknowledgement);
public sealed record NotificationHistory(long Version, string State, string Action, DateTimeOffset At, string ActorId, string ClientId, string CorrelationId);
public sealed record DriverNotification(Guid Id, string TenantId, Guid IncidentId, Guid DriverId, string OwnerId,
    string SourceKind, Guid SourceId, Guid FaultId, long PublicationVersion, long OntologyVersion,
    FaultRemedy Remedy, ImmutableArray<NotificationEvidence> Evidence, string TemplateVersion, string Message,
    string Contact, long DriverVersion, long IncidentVersion, string State, long Version, DateTimeOffset CreatedAt,
    DateTimeOffset? NextAttemptAt, Guid? LeaseId, DateTimeOffset? LeaseUntil,
    ImmutableArray<NotificationAttempt> Attempts, ImmutableArray<NotificationHistory> History);
public static class NotificationRules
{
    public const string Template = "driver-notification-v1";
    public static bool Contact(string? value) => EmailContact(value) || SyntheticContact(value);
    private static bool EmailContact(string? value) => value is { Length: <= 150 }
        && System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-zA-Z0-9._+-]+@example\\.com$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private static bool SyntheticContact(string? value) => value is { Length: <= 60 } && value.StartsWith("SYN-", StringComparison.Ordinal)
        && value.Length > 4 && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
    public static string Render(Guid incident, string kind, FaultRemedy remedy) =>
        $"Synthetic incident {incident:D}. {remedy.Description}"
        + (kind == "dealership-referral" ? $" Contact a dealership for assessment. Approved patch reference: {remedy.Patch!.Reference}. This is a recommendation; no vehicle software has been installed." : "");
}
