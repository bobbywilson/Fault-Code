using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace FaultCode.Domain;

public sealed record OntologyConcept(string Id, string Kind, string System, string Label, ImmutableArray<string> AllowedRemedyKinds);
public sealed record OntologyRelationship(string Id, string Kind, string From, string To);
public sealed record OntologyRule(string Id, string System, ImmutableArray<string> Symptoms, string ClassificationId,
    string SeverityId, ImmutableArray<string> RemedyKinds, int Priority, string? Model, string? SoftwareVersion);
public sealed record OntologyContent(ImmutableArray<OntologyConcept> Concepts, ImmutableArray<OntologyRelationship> Relationships,
    ImmutableArray<OntologyRule> Rules);
public sealed record OntologyInput(long PublicationVersion, string System, ImmutableArray<string> Symptoms, string Model, string SoftwareVersion);
public sealed record OntologyOutcome(string? RuleId, string? ClassificationId, string? SeverityId, ImmutableArray<string> RemedyKinds,
    ImmutableArray<string> MatchedRuleIds, string Explanation);
public sealed record OntologyRevision(long Version, OntologyContent Content, string State, string ActorId, DateTimeOffset At);
public sealed record OntologyPublication(long Version, OntologyContent Content, string ApprovedBy, DateTimeOffset ApprovedAt);
public sealed record OntologyCatalog(Guid Id, long Version, OntologyContent Draft, ImmutableArray<OntologyRevision> History,
    ImmutableArray<OntologyPublication> Publications);
public sealed record OntologyEvaluation(Guid Id, long PublicationVersion, OntologyInput Input, OntologyOutcome Outcome,
    string EvaluatedBy, DateTimeOffset EvaluatedAt, string ApprovedBy, DateTimeOffset ApprovedAt);

public static class OntologyRules
{
    public static bool System(string? value) => value is "EPS" or "steer-by-wire" or "steering-ADAS";
    private static bool Id(string? value) => value is not null && value.Length <= 100 && !value.Any(char.IsControl)
        && Regex.IsMatch(value, "^SYN-[A-Za-z0-9][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant);
    private static bool Applicability(string? model, string? software) => FaultRules.SyntheticModel(model) && FaultRules.Software(software)
        && !model!.Any(char.IsControl) && !software!.Any(char.IsControl);
    private static bool Kinds(ImmutableArray<string> kinds) => !kinds.IsDefault && kinds.Length <= 2
        && kinds.All(x => x is "guidance" or "dealership") && kinds.Distinct(StringComparer.Ordinal).Count() == kinds.Length;
    private static void Require(bool valid) { if (!valid) throw new ArgumentException("Invalid ontology"); }
    public static int Specificity(OntologyRule r) => r.Symptoms.Length + (r.Model is null ? 0 : 1);
    private static bool SameOutcome(OntologyRule a, OntologyRule b) => a.ClassificationId == b.ClassificationId
        && a.SeverityId == b.SeverityId && a.RemedyKinds.Order(StringComparer.Ordinal).SequenceEqual(b.RemedyKinds.Order(StringComparer.Ordinal));
    public static void Validate(OntologyContent c)
    {
        Require(!c.Concepts.IsDefault && c.Concepts.Length is >= 1 and <= 200 && !c.Relationships.IsDefault
            && c.Relationships.Length <= 400 && !c.Rules.IsDefault && c.Rules.Length <= 100);
        Require(c.Concepts.All(x => x is not null && Id(x.Id) && System(x.System) && FaultRules.Text(x.Label, 200)
            && x.Kind is "subsystem" or "component" or "symptom" or "cause" or "classification" or "severity" or "applicability" or "remedy"
            && Kinds(x.AllowedRemedyKinds) && (x.Kind == "classification" || x.AllowedRemedyKinds.Length == 0)));
        Require(c.Concepts.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == c.Concepts.Length);
        var concepts = c.Concepts.ToDictionary(x => x.Id, StringComparer.Ordinal);
        Require(c.Relationships.All(x => x is not null && Id(x.Id) && x.From is not null && x.To is not null));
        Require(c.Relationships.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == c.Relationships.Length
            && c.Relationships.Select(x => (x.Kind, x.From, x.To)).Distinct().Count() == c.Relationships.Length);
        foreach (var edge in c.Relationships)
        {
            Require(concepts.ContainsKey(edge.From) && concepts.ContainsKey(edge.To) && edge.From != edge.To);
            var from = concepts[edge.From]; var to = concepts[edge.To];
            Require(from.System == to.System && (edge.Kind switch
            {
                "is-a" => from.Kind == to.Kind,
                "part-of" => from.Kind is "component" or "subsystem" && to.Kind == "subsystem",
                "indicates" => from.Kind == "symptom" && to.Kind == "classification",
                "causes" => from.Kind == "cause" && to.Kind is "symptom" or "classification",
                "classifies" => from.Kind == "component" && to.Kind == "classification",
                "mitigates" => from.Kind == "remedy" && to.Kind == "classification",
                "applies-to" => from.Kind == "applicability" && to.Kind is "component" or "subsystem",
                _ => false
            }));
        }
        // Check the combined containment/inheritance graph, including mixed-edge cycles.
        var active = new HashSet<string>(StringComparer.Ordinal); var done = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            if (done.Contains(id)) return;
            Require(active.Add(id));
            foreach (var edge in c.Relationships.Where(e => e.From == id && e.Kind is "is-a" or "part-of")) Visit(edge.To);
            active.Remove(id); done.Add(id);
        }
        foreach (var id in concepts.Keys) Visit(id);
        Require(c.Rules.All(r => r is not null && Id(r.Id) && System(r.System) && !r.Symptoms.IsDefault
            && r.Symptoms.Length is >= 1 and <= 20 && r.Symptoms.All(Id)
            && r.Symptoms.Distinct(StringComparer.Ordinal).Count() == r.Symptoms.Length && Kinds(r.RemedyKinds)
            && r.Priority is >= 0 and <= 1000 && Id(r.ClassificationId) && Id(r.SeverityId)
            && (r.Model is null && r.SoftwareVersion is null || Applicability(r.Model, r.SoftwareVersion))));
        Require(c.Rules.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() == c.Rules.Length);
        bool Concept(string id, string kind, string system) => concepts.TryGetValue(id, out var item) && item.Kind == kind && item.System == system;
        foreach (var rule in c.Rules)
        {
            Require(Concept(rule.ClassificationId, "classification", rule.System) && Concept(rule.SeverityId, "severity", rule.System)
                && rule.Symptoms.All(s => Concept(s, "symptom", rule.System)));
            Require(rule.RemedyKinds.All(k => concepts[rule.ClassificationId].AllowedRemedyKinds.Contains(k, StringComparer.Ordinal)));
        }
        for (var i = 0; i < c.Rules.Length; i++)
            for (var j = i + 1; j < c.Rules.Length; j++)
            {
                var a = c.Rules[i]; var b = c.Rules[j];
                var overlap = a.System == b.System && (a.Model is null || b.Model is null || a.Model == b.Model && a.SoftwareVersion == b.SoftwareVersion);
                // Symptom predicates are positive conjunctions: disjoint sets can still match together.
                Require(!overlap || a.Priority != b.Priority || Specificity(a) != Specificity(b) || SameOutcome(a, b));
            }
    }
    public static OntologyContent Canonical(OntologyContent c)
    {
        Validate(c);
        return new([.. c.Concepts.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => x with { AllowedRemedyKinds = [.. x.AllowedRemedyKinds.Order(StringComparer.Ordinal)] })],
            [.. c.Relationships.OrderBy(x => x.Id, StringComparer.Ordinal)],
            [.. c.Rules.OrderBy(x => x.Id, StringComparer.Ordinal).Select(r => r with { Symptoms = [.. r.Symptoms.Order(StringComparer.Ordinal)], RemedyKinds = [.. r.RemedyKinds.Order(StringComparer.Ordinal)] })]);
    }
    public static OntologyInput Canonical(OntologyInput input)
    {
        Require(input.PublicationVersion > 0 && System(input.System) && !input.Symptoms.IsDefault && input.Symptoms.Length is >= 1 and <= 20
            && input.Symptoms.All(Id) && input.Symptoms.Distinct(StringComparer.Ordinal).Count() == input.Symptoms.Length
            && Applicability(input.Model, input.SoftwareVersion));
        return input with { Symptoms = [.. input.Symptoms.Order(StringComparer.Ordinal)] };
    }
    public static OntologyOutcome Evaluate(OntologyContent content, OntologyInput input)
    {
        input = Canonical(input);
        Require(input.Symptoms.All(id => content.Concepts.Any(c => c.Id == id && c.Kind == "symptom" && c.System == input.System)));
        var matched = content.Rules.Where(r => r.System == input.System && r.Symptoms.All(s => input.Symptoms.Contains(s, StringComparer.Ordinal))
            && (r.Model is null || r.Model == input.Model && r.SoftwareVersion == input.SoftwareVersion))
            .OrderByDescending(r => r.Priority).ThenByDescending(Specificity).ThenBy(r => r.Id, StringComparer.Ordinal).ToArray();
        var winner = matched.FirstOrDefault();
        return new(winner?.Id, winner?.ClassificationId, winner?.SeverityId, winner?.RemedyKinds ?? [], [.. matched.Select(r => r.Id)],
            winner is null ? "No approved rule matched the supplied symptoms and applicability."
                : $"Rule {winner.Id} matched all required symptoms and applicability; priority {winner.Priority}, specificity {Specificity(winner)}. Priority then specificity then ordinal ID determines precedence.");
    }
}
