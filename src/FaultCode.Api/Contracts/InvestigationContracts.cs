using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FaultCode.Application.Investigations;

namespace FaultCode.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InvestigationRequest;
public sealed class InvestigationQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}
public sealed record InvestigationRuleResponse(long PublicationVersion, string ApprovedBy, DateTimeOffset ApprovedAt,
    EvaluateOntologyRequest Input, OntologyOutcomeResponse Outcome);
public sealed record InvestigationModelResponse(string Model, string InstructionsVersion, int Attempts, long InputTokens,
    long OutputTokens, int ReservedTokens, bool ReviewRequired);
public sealed record InvestigationProposalResponse(string Kind, string Status);
public sealed record InvestigationResponse(Guid Id, Guid IncidentId, long IncidentVersion, DateTimeOffset CreatedAt,
    string CorrelationId, string Classification, ApprovedFaultResponse? Definition, InvestigationRuleResponse? Rule,
    EvidenceCitationResponse[] Citations, RemedyRequest? Remedy, string[] Uncertainties,
    InvestigationProposalResponse ProposedAction, InvestigationModelResponse Model, string PolicyVersion, string EvidencePolicy)
{
    public static InvestigationResponse From(InvestigationResult r)
    {
        var f = r.Definition;
        FaultContentRequest Content(FaultCode.Domain.FaultContent c) => new(c.Description, c.ClassificationId, c.ClassificationVersion,
            c.Symptoms.ToArray(), c.References.Select(x => new ReferenceRequest(x.Id, x.Version)).ToArray(), c.Remedies.Select(MapRemedy).ToArray());
        return new(r.Id, r.IncidentId, r.IncidentVersion, r.CreatedAt, r.CorrelationId, r.Classification,
            f is null ? null : new(f.Id, f.Code, f.System, f.Version, Content(f.Content), f.ApprovedBy, f.ApprovedAt),
            r.Rule is not { } rule ? null : new(rule.PublicationVersion, rule.ApprovedBy, rule.ApprovedAt,
                new(rule.Input.PublicationVersion, rule.Input.System, rule.Input.Symptoms.ToArray(), rule.Input.Model, rule.Input.SoftwareVersion),
                new(rule.Outcome.RuleId, rule.Outcome.ClassificationId, rule.Outcome.SeverityId, rule.Outcome.RemedyKinds.ToArray(), rule.Outcome.MatchedRuleIds.ToArray(), rule.Outcome.Explanation)),
            EvidenceResponse.From(new("historical-evidence", "Untrusted historical evidence; not current dispatch authority.", r.Citations)).Citations,
            r.Remedy is null ? null : MapRemedy(r.Remedy), r.Uncertainties.ToArray(), new(r.ProposedAction.Kind, r.ProposedAction.Status),
            new(r.Model.Model, r.Model.InstructionsVersion, r.Model.Attempts, r.Model.InputTokens, r.Model.OutputTokens, r.Model.ReservedTokens, r.Model.ReviewRequired), r.PolicyVersion, "Untrusted historical source evidence; pending proposal only, no current dispatch authority.");
    }
    private static RemedyRequest MapRemedy(FaultCode.Domain.FaultRemedy r) => new(r.Id, r.Kind, r.Description, r.Approved,
        r.Model, r.SoftwareVersion, r.Patch is null ? null : new(r.Patch.Reference, r.Patch.SoftwareVersion));
}
