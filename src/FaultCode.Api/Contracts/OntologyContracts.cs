using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FaultCode.Domain;

namespace FaultCode.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OntologyContentRequest([Required] ConceptRequest[] Concepts, [Required] RelationshipRequest[] Relationships, [Required] RuleRequest[] Rules)
{
    public OntologyContent ToDomain() => new(
        [.. Concepts.Select(x => x is null ? null! : new OntologyConcept(x.Id, x.Kind, x.System, x.Label, x.AllowedRemedyKinds is null ? default : [.. x.AllowedRemedyKinds]))],
        [.. Relationships.Select(x => x is null ? null! : new OntologyRelationship(x.Id, x.Kind, x.From, x.To))],
        [.. Rules.Select(x => x is null ? null! : new OntologyRule(x.Id, x.System, x.Symptoms is null ? default : [.. x.Symptoms], x.ClassificationId,
            x.SeverityId, x.RemedyKinds is null ? default : [.. x.RemedyKinds], x.Priority, x.Model, x.SoftwareVersion))]);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConceptRequest([Required] string Id, [Required] string Kind, [Required] string System, [Required] string Label, [Required] string[] AllowedRemedyKinds);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RelationshipRequest([Required] string Id, [Required] string Kind, [Required] string From, [Required] string To);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RuleRequest([Required] string Id, [Required] string System, [Required] string[] Symptoms, [Required] string ClassificationId,
    [Required] string SeverityId, [Required] string[] RemedyKinds, int Priority, string? Model, string? SoftwareVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EvaluateOntologyRequest(long PublicationVersion, [Required] string System, [Required] string[] Symptoms, [Required] string Model, [Required] string SoftwareVersion)
{
    public OntologyInput ToDomain() => new(PublicationVersion, System, Symptoms.ToImmutableArray(), Model, SoftwareVersion);
}
public sealed record OntologyDraftResponse(Guid Id, long Version, OntologyContentRequest Draft);
public sealed record OntologyRevisionResponse(long Version, OntologyContentRequest Content, string State, string ActorId, DateTimeOffset At);
public sealed record OntologyPublicationResponse(long Version, OntologyContentRequest Content, string ApprovedBy, DateTimeOffset ApprovedAt);
public sealed record OntologyOutcomeResponse(string? RuleId, string? ClassificationId, string? SeverityId, string[] RemedyKinds, string[] MatchedRuleIds, string Explanation);
public sealed record OntologyEvaluationResponse(Guid Id, long PublicationVersion, EvaluateOntologyRequest Input, OntologyOutcomeResponse Outcome,
    string EvaluatedBy, DateTimeOffset EvaluatedAt, string ApprovedBy, DateTimeOffset ApprovedAt);
internal static class OntologyResponses
{
    internal static OntologyContentRequest Content(OntologyContent c) => new(
        c.Concepts.Select(x => new ConceptRequest(x.Id, x.Kind, x.System, x.Label, x.AllowedRemedyKinds.ToArray())).ToArray(),
        c.Relationships.Select(x => new RelationshipRequest(x.Id, x.Kind, x.From, x.To)).ToArray(),
        c.Rules.Select(x => new RuleRequest(x.Id, x.System, x.Symptoms.ToArray(), x.ClassificationId, x.SeverityId, x.RemedyKinds.ToArray(), x.Priority, x.Model, x.SoftwareVersion)).ToArray());
    internal static OntologyPublicationResponse Publication(OntologyPublication p) => new(p.Version, Content(p.Content), p.ApprovedBy, p.ApprovedAt);
    internal static OntologyRevisionResponse Revision(OntologyRevision r) => new(r.Version, Content(r.Content), r.State, r.ActorId, r.At);
    internal static OntologyEvaluationResponse Evaluation(OntologyEvaluation e) => new(e.Id, e.PublicationVersion,
        new(e.Input.PublicationVersion, e.Input.System, e.Input.Symptoms.ToArray(), e.Input.Model, e.Input.SoftwareVersion),
        new(e.Outcome.RuleId, e.Outcome.ClassificationId, e.Outcome.SeverityId, e.Outcome.RemedyKinds.ToArray(), e.Outcome.MatchedRuleIds.ToArray(), e.Outcome.Explanation),
        e.EvaluatedBy, e.EvaluatedAt, e.ApprovedBy, e.ApprovedAt);
}
