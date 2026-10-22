using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FaultCode.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateIncidentRequest(Guid VehicleId, Guid DriverId, [Required] string SourceEventId,
    [Required] string Code, [Required] string System, [Required] string Model, [Required] string SoftwareVersion, DateTimeOffset OccurredAt);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IncidentStatusRequest([Required] string Status);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IncidentFailureRequest([Required] string Code);
public sealed class IncidentQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public Guid? VehicleId { get; set; }
    public bool UnmatchedOnly { get; set; }
}
public sealed record IncidentFleetResponse(Guid VehicleId, string VehicleIdentifier, long VehicleVersion, Guid DriverId,
    string DriverIdentifier, long DriverVersion, Guid AssociationId, DateTimeOffset AssociationFrom, DateTimeOffset? AssociationUntil);
public sealed record IncidentKnowledgeResponse(Guid DefinitionId, long PublicationVersion, FaultContentRequest Content, string ApprovedBy, DateTimeOffset ApprovedAt);
public sealed record IncidentWorkResponse(Guid Id, string Kind, string State, int Attempts, string? FailureCode);
public sealed record IncidentHistoryResponse(long Version, string Status, string Action, string ActorId, string ClientId,
    string CorrelationId, DateTimeOffset At, string? FailureCode);
public sealed record IncidentResponse(Guid Id, string SourceClientId, string SourceEventId, string Code, string System,
    string Model, string SoftwareVersion, DateTimeOffset OccurredAt, DateTimeOffset ReceivedAt, string IntakeCorrelationId,
    IncidentFleetResponse Fleet, IncidentKnowledgeResponse? Knowledge, string Status, long Version, IncidentWorkResponse[] Work);
