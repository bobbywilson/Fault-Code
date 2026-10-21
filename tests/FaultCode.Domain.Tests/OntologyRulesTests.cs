using FaultCode.Domain;

namespace FaultCode.Domain.Tests;

public sealed class OntologyRulesTests
{
    private static OntologyContent Content => new([
        new("SYN-SUB", "subsystem", "EPS", "Synthetic EPS", []), new("SYN-COMP", "component", "EPS", "Synthetic component", []),
        new("SYN-SYM", "symptom", "EPS", "Synthetic symptom", []), new("SYN-CLASS", "classification", "EPS", "Synthetic classification", ["dealership"]),
        new("SYN-SEV", "severity", "EPS", "Synthetic severity", []), new("SYN-APP", "applicability", "EPS", "Synthetic applicability", []),
        new("SYN-REM", "remedy", "EPS", "Synthetic remedy", [])], [new("SYN-EDGE", "part-of", "SYN-COMP", "SYN-SUB")],
        [new("SYN-RULE", "EPS", ["SYN-SYM"], "SYN-CLASS", "SYN-SEV", ["dealership"], 10, null, null)]);
    private static readonly OntologyInput Input = new(2, "EPS", ["SYN-SYM"], "SYN-MODEL-1", "1.0");
    [Theory]
    [InlineData("EPS")]
    [InlineData("steer-by-wire")]
    [InlineData("steering-ADAS")]
    public void All_seven_concept_kinds_and_synthetic_systems_are_supported(string system)
    {
        var c = Content with { Concepts = [.. Content.Concepts.Select(x => x with { System = system })], Rules = [Content.Rules[0] with { System = system }] };
        OntologyRules.Validate(c); var outcome = OntologyRules.Evaluate(c, Input with { System = system });
        Assert.Equal("SYN-RULE", outcome.RuleId); Assert.Contains("priority 10", outcome.Explanation);
    }
    [Theory]
    [InlineData("part-of", "SYN-COMP", "SYN-SUB")]
    [InlineData("indicates", "SYN-SYM", "SYN-CLASS")]
    [InlineData("classifies", "SYN-COMP", "SYN-CLASS")]
    [InlineData("mitigates", "SYN-REM", "SYN-CLASS")]
    [InlineData("applies-to", "SYN-APP", "SYN-COMP")]
    public void Declared_relationship_types_accept_only_their_typed_references(string kind, string from, string to)
    { OntologyRules.Validate(Content with { Relationships = [new("SYN-E", kind, from, to)] }); }
    [Fact]
    public void Invalid_references_types_duplicates_and_cycles_reject()
    {
        var c = Content;
        var invalid = new[] {
            c with { Concepts = c.Concepts.Add(c.Concepts[0]) },
            c with { Concepts = [c.Concepts[0] with { System = "brakes" }] },
            c with { Concepts = [c.Concepts[0] with { Kind = "unknown" }] },
            c with { Concepts = [c.Concepts[0] with { Id = "SYN-NEWLINE\n" }] },
            c with { Relationships = [new("SYN-E", "part-of", "SYN-MISSING", "SYN-SUB")] },
            c with { Relationships = [new("SYN-E", "unknown", "SYN-COMP", "SYN-SUB")] },
            c with { Concepts = [.. c.Concepts.Select(x => x.Id == "SYN-COMP" ? x with { System = "steering-ADAS" } : x)] },
            c with { Relationships = c.Relationships.Add(c.Relationships[0]) },
            c with { Rules = c.Rules.Add(c.Rules[0]) },
            c with { Relationships = [new("SYN-E", "part-of", "SYN-SYM", "SYN-SUB")] },
            c with { Relationships = [new("SYN-E", "part-of", "SYN-SUB", "SYN-SUB")] },
            c with { Relationships = [c.Relationships[0], c.Relationships[0] with { Id = "SYN-E2" }] },
            c with { Rules = [c.Rules[0] with { SeverityId = "SYN-MISSING" }] },
            c with { Rules = [c.Rules[0] with { ClassificationId = "SYN-SUB" }] },
            c with { Rules = [c.Rules[0] with { Symptoms = ["SYN-COMP"] }] },
            c with { Rules = [c.Rules[0] with { RemedyKinds = ["guidance"] }] },
            c with { Rules = [c.Rules[0] with { Model = "SYN-MODEL-1" }] },
            c with { Rules = [c.Rules[0] with { Model = "SYN-MODEL-1\n", SoftwareVersion = "1.0" }] },
            c with { Concepts = c.Concepts.Add(new("SYN-SUB2", "subsystem", "EPS", "Other", [])),
                Relationships = [new("SYN-E1", "is-a", "SYN-SUB", "SYN-SUB2"), new("SYN-E2", "part-of", "SYN-SUB2", "SYN-SUB")] }
        };
        foreach (var item in invalid) Assert.Throws<ArgumentException>(() => OntologyRules.Validate(item));
        Assert.Throws<ArgumentException>(() => OntologyRules.Validate(c with { Concepts = default }));
        Assert.Throws<ArgumentException>(() => OntologyRules.Evaluate(c, Input with { Symptoms = ["SYN-MISSING"] }));
        Assert.Throws<ArgumentException>(() => OntologyRules.Evaluate(c, Input with { System = "steering-ADAS" }));
    }
    [Fact]
    public void Priority_specificity_and_ordinal_ties_are_deterministic_and_no_match_is_explicit()
    {
        var basic = Content.Rules[0]; var specific = basic with { Id = "SYN-SPECIFIC", Model = "SYN-MODEL-1", SoftwareVersion = "1.0" };
        var c = Content with { Rules = [basic, specific] }; OntologyRules.Validate(c);
        Assert.Equal(specific.Id, OntologyRules.Evaluate(c, Input).RuleId);
        c = c with { Rules = [basic with { Priority = 20 }, specific] }; OntologyRules.Validate(c);
        Assert.Equal(basic.Id, OntologyRules.Evaluate(c, Input).RuleId);
        c = c with { Rules = [basic with { Id = "SYN-Z" }, basic with { Id = "SYN-A" }] }; OntologyRules.Validate(c);
        Assert.Equal("SYN-A", OntologyRules.Evaluate(c, Input).RuleId);
        c = c with { Rules = [specific] };
        Assert.Null(OntologyRules.Evaluate(c, Input with { Model = "SYN-MODEL-2" }).RuleId);
    }
    [Fact]
    public void Contradictions_are_rejected_even_for_different_positive_symptom_sets()
    {
        var c = Content with { Concepts = Content.Concepts.Add(new("SYN-SYM2", "symptom", "EPS", "Other", [])) };
        var a = c.Rules[0]; var b = a with { Id = "SYN-OTHER", Symptoms = ["SYN-SYM2"], RemedyKinds = [] };
        Assert.Throws<ArgumentException>(() => OntologyRules.Validate(c with { Rules = [a, b] }));
        OntologyRules.Validate(c with { Rules = [a, b with { Priority = 11 }] });
        OntologyRules.Validate(c with { Rules = [a with { Model = "SYN-MODEL-1", SoftwareVersion = "1" }, b with { Model = "SYN-MODEL-2", SoftwareVersion = "1" }] });
    }
    [Fact]
    public void Null_entries_and_collection_bounds_fail_safely()
    {
        Assert.Throws<ArgumentException>(() => OntologyRules.Validate(Content with { Concepts = [null!] }));
        Assert.Throws<ArgumentException>(() => OntologyRules.Validate(Content with { Rules = [null!] }));
        Assert.Throws<ArgumentException>(() => OntologyRules.Validate(Content with { Relationships = [null!] }));
        Assert.Throws<ArgumentException>(() => OntologyRules.Validate(Content with { Concepts = [.. Enumerable.Repeat(Content.Concepts[0], 201)] }));
        Assert.Throws<ArgumentException>(() => OntologyRules.Canonical(Input with { Symptoms = ["SYN-SYM", "SYN-SYM"] }));
        Assert.Throws<ArgumentException>(() => OntologyRules.Canonical(Input with { SoftwareVersion = "1.0\n" }));
    }
}
