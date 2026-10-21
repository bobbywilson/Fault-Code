using System.Collections.Immutable;

namespace FaultCode.Domain;

public sealed record EngineeringNote(Guid Id, string Text, string ActorId, string ClientId, DateTimeOffset At);
public sealed record ResolutionProposal(Guid Id, FaultContent Content, long ExpectedFaultVersion, long IncidentVersion,
    string ProposedBy, DateTimeOffset At, ImmutableArray<ResolutionEvidence> Evidence);
public sealed record ResolutionEvidence(Guid DocumentId, string SourceId, string SourceVersion, long PublicationVersion, string Checksum);
public sealed record EngineeringApproval(Guid Id, Guid ProposalId, Guid FaultId, long PublicationVersion, long OntologyVersion,
    ImmutableArray<ResolutionEvidence> Evidence, Guid IncidentId, Guid DriverId, string ApprovedBy, DateTimeOffset At, Guid EventId);
public sealed record EngineeringHistory(long Version, string Status, string Action, string? AssigneeId, EngineeringNote? Note,
    ResolutionProposal? Proposal, Guid? ApprovalId, string ActorId, string ClientId, string CorrelationId, DateTimeOffset At);
public sealed record EngineeringCase(Guid Id, string TenantId, Guid IncidentId, Guid InvestigationId, string Classification,
    long InvestigationIncidentVersion, string Status, long Version, string? AssigneeId, ResolutionProposal? Proposal,
    ImmutableArray<EngineeringNote> Notes, ImmutableArray<EngineeringApproval> Approvals, ImmutableArray<EngineeringHistory> History);
public sealed record EngineeringEvent(Guid Id, Guid CaseId, Guid ApprovalId, Guid IncidentId, long IncidentVersion, Guid DriverId,
    Guid FaultId, long PublicationVersion, DateTimeOffset CreatedAt, string State);
public static class EngineeringRules
{
    public static bool Status(string? status) => status is "Open" or "Investigating" or "AwaitingApproval" or "Resolved" or "Reopened";
    public static bool Transition(string from, string to) => (from, to) is ("Open", "Investigating")
        or ("Reopened", "Investigating") or ("AwaitingApproval", "Investigating") or ("Resolved", "Reopened");
}
