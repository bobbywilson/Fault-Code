using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace FaultCode.Domain;

public sealed record FaultReference(string Id, string Version);
public sealed record PatchAdvisory(string Reference, string SoftwareVersion);
public sealed record FaultRemedy(string Id, string Kind, string Description, bool Approved,
    string Model, string SoftwareVersion, PatchAdvisory? Patch);
public sealed record FaultContent(string Description, string ClassificationId, long ClassificationVersion,
    ImmutableArray<string> Symptoms, ImmutableArray<FaultReference> References, ImmutableArray<FaultRemedy> Remedies);
public sealed record FaultRevision(long Version, FaultContent Content, string State, string ActorId, DateTimeOffset At);
public sealed record FaultPublication(long Version, FaultContent Content, string ApprovedBy, DateTimeOffset ApprovedAt);
public sealed record FaultDefinition(Guid Id, string TenantId, string Code, string System, bool Retired, long Version,
    FaultContent Draft, ImmutableArray<FaultRevision> History, ImmutableArray<FaultPublication> Publications);

public static class FaultRules
{
    public static string Code(string? code, string system)
    {
        var prefix = system switch { "EPS" => "SYN-EPS:", "steer-by-wire" => "SYN-SBW:", "steering-ADAS" => "SYN-ADAS:", _ => throw new ArgumentException("Invalid system") };
        var normalized = code?.Trim().ToUpperInvariant();
        if (normalized is null || normalized.Length > 60 || !normalized.StartsWith(prefix, StringComparison.Ordinal)
            || !Regex.IsMatch(normalized[prefix.Length..], "^[A-Z0-9][A-Z0-9-]{0,39}$", RegexOptions.CultureInvariant)) throw new ArgumentException("Invalid code");
        return normalized;
    }
    public static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max && !value.Any(char.IsControl);
    public static bool DemoModel(string? value) => value is not null && value.Length <= 40 && Regex.IsMatch(value, "^SYN-[A-Z0-9-]+$", RegexOptions.CultureInvariant);
    public static bool Software(string? value) => value is not null && value.Length <= 40 && Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant);
    public static void Validate(FaultContent c)
    {
        if (!Text(c.Description, 1000) || !Text(c.ClassificationId, 100) || c.ClassificationVersion < 1
            || c.Symptoms.IsDefault || c.Symptoms.Length is < 1 or > 20 || c.Symptoms.Any(s => !Text(s, 200))
            || c.References.IsDefault || c.References.Length is < 1 or > 20 || c.References.Any(x => x is null || !Text(x.Id, 100) || !Text(x.Version, 40))
            || c.Remedies.IsDefault || c.Remedies.Length > 20 || c.Remedies.Any(x => x is null)
            || c.Remedies.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != c.Remedies.Length) throw new ArgumentException("Invalid definition");
        foreach (var remedy in c.Remedies)
            if (!Text(remedy.Id, 80) || remedy.Kind is not ("guidance" or "dealership") || !Text(remedy.Description, 1000)
                || !DemoModel(remedy.Model) || !Software(remedy.SoftwareVersion)
                || remedy.Patch is { } patch && (remedy.Kind != "dealership" || !Text(patch.Reference, 100)
                    || !Software(patch.SoftwareVersion) || patch.SoftwareVersion != remedy.SoftwareVersion)) throw new ArgumentException("Invalid remedy");
    }
}
