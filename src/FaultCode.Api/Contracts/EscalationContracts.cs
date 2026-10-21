using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FaultCode.Domain;

namespace FaultCode.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateEngineeringCaseRequest(Guid IncidentId, Guid InvestigationId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EngineeringAssignmentRequest(string? AssigneeId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EngineeringNoteRequest([Required, MaxLength(2000)] string Text);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EngineeringStatusRequest([Required] string Status);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EngineeringResolutionRequest([Required] FaultContentRequest Content, [Range(0, long.MaxValue)] long ExpectedFaultVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EngineeringApprovalRequest;
public sealed class EngineeringQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
}
public sealed record EngineeringNoteResponse(Guid Id, string Text, string ActorId, string ClientId, DateTimeOffset At)
{
    public static EngineeringNoteResponse From(EngineeringNote n) => new(n.Id, n.Text, n.ActorId, n.ClientId, n.At);
}
public sealed record EngineeringProposalResponse(Guid Id, FaultContentRequest Content, long ExpectedFaultVersion,
    long IncidentVersion, string ProposedBy, DateTimeOffset At, EngineeringEvidenceResponse[] Evidence)
{
    public static EngineeringProposalResponse? From(ResolutionProposal? p) => p is null ? null : new(p.Id, ContentFrom(p.Content), p.ExpectedFaultVersion, p.IncidentVersion, p.ProposedBy, p.At,
        p.Evidence.Select(e => new EngineeringEvidenceResponse(e.DocumentId, e.SourceId, e.SourceVersion, e.PublicationVersion, e.Checksum)).ToArray());
    internal static FaultContentRequest ContentFrom(FaultContent c) => new(c.Description, c.ClassificationId, c.ClassificationVersion,
        c.Symptoms.ToArray(), c.References.Select(r => new ReferenceRequest(r.Id, r.Version)).ToArray(),
        c.Remedies.Select(r => new RemedyRequest(r.Id, r.Kind, r.Description, r.Approved, r.Model, r.SoftwareVersion,
            r.Patch is null ? null : new PatchRequest(r.Patch.Reference, r.Patch.SoftwareVersion))).ToArray());
}
public sealed record EngineeringEvidenceResponse(Guid DocumentId, string SourceId, string SourceVersion, long PublicationVersion, string Checksum);
public sealed record EngineeringApprovalResponse(Guid Id, Guid ProposalId, Guid FaultId, long PublicationVersion, long OntologyVersion,
    EngineeringEvidenceResponse[] Evidence, Guid IncidentId, Guid DriverId, string ApprovedBy, DateTimeOffset At, Guid EventId)
{
    public static EngineeringApprovalResponse From(EngineeringApproval a) => new(a.Id, a.ProposalId, a.FaultId, a.PublicationVersion, a.OntologyVersion,
        a.Evidence.Select(e => new EngineeringEvidenceResponse(e.DocumentId, e.SourceId, e.SourceVersion, e.PublicationVersion, e.Checksum)).ToArray(),
        a.IncidentId, a.DriverId, a.ApprovedBy, a.At, a.EventId);
}
public sealed record EngineeringHistoryResponse(long Version, string Status, string Action, string? AssigneeId,
    EngineeringNoteResponse? Note, EngineeringProposalResponse? Proposal, Guid? ApprovalId, string ActorId, string ClientId, string CorrelationId, DateTimeOffset At)
{
    public static EngineeringHistoryResponse From(EngineeringHistory h) => new(h.Version, h.Status, h.Action, h.AssigneeId,
        h.Note is null ? null : EngineeringNoteResponse.From(h.Note), EngineeringProposalResponse.From(h.Proposal), h.ApprovalId, h.ActorId, h.ClientId, h.CorrelationId, h.At);
}
public sealed record EngineeringCaseResponse(Guid Id, Guid IncidentId, Guid InvestigationId, string Classification,
    long InvestigationIncidentVersion, string Status, long Version, string? AssigneeId, EngineeringProposalResponse? Proposal,
    EngineeringNoteResponse[] Notes, EngineeringApprovalResponse[] Approvals)
{
    public static EngineeringCaseResponse From(EngineeringCase e) => new(e.Id, e.IncidentId, e.InvestigationId, e.Classification,
        e.InvestigationIncidentVersion, e.Status, e.Version, e.AssigneeId, EngineeringProposalResponse.From(e.Proposal),
        e.Notes.Select(EngineeringNoteResponse.From).ToArray(), e.Approvals.Select(EngineeringApprovalResponse.From).ToArray());
}
public sealed record EngineeringEventResponse(Guid Id, Guid CaseId, Guid ApprovalId, Guid IncidentId, long IncidentVersion, Guid DriverId,
    Guid FaultId, long PublicationVersion, DateTimeOffset CreatedAt, string State, string DispatchPolicy)
{
    public static EngineeringEventResponse From(EngineeringEvent e) => new(e.Id, e.CaseId, e.ApprovalId, e.IncidentId, e.IncidentVersion, e.DriverId,
        e.FaultId, e.PublicationVersion, e.CreatedAt, e.State,
        "Historical intent only. FAU-19 must revalidate current case/incident approval, applicable knowledge and consent, and deduplicate by event ID before delivery.");
}
