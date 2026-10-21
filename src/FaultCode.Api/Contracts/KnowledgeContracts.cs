using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FaultCode.Domain;
namespace FaultCode.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FaultContentRequest([Required] string Description, [Required] string ClassificationId, long ClassificationVersion,
    [Required] string[] Symptoms, [Required] ReferenceRequest[] References, [Required] RemedyRequest[] Remedies)
{
    public FaultContent ToDomain() => new(Description, ClassificationId, ClassificationVersion,
        Symptoms.ToImmutableArray(), References.Select(r => r is null ? null! : new FaultReference(r.Id, r.Version)).ToImmutableArray(),
        Remedies.Select(r => r is null ? null! : new FaultRemedy(r.Id, r.Kind, r.Description, r.Approved, r.Model, r.SoftwareVersion,
            r.Patch is null ? null : new PatchAdvisory(r.Patch.Reference, r.Patch.SoftwareVersion))).ToImmutableArray());
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateFaultRequest([Required] string Code, [Required] string System, [Required] FaultContentRequest Content);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReferenceRequest([Required] string Id, [Required] string Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PatchRequest([Required] string Reference, [Required] string SoftwareVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RemedyRequest([Required] string Id, [Required] string Kind, [Required] string Description, bool Approved,
    [Required] string Model, [Required] string SoftwareVersion, PatchRequest? Patch);
public sealed class FaultQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    public string? System { get; set; }
    [MaxLength(100)] public string? Search { get; set; }
    public bool? Retired { get; set; }
}
public sealed record FaultResponse(Guid Id, string Code, string System, bool Retired, long Version, FaultContentRequest Draft);
public sealed record FaultRevisionResponse(long Version, FaultContentRequest Content, string State, string ActorId, DateTimeOffset At);
public sealed record ApprovedFaultResponse(Guid Id, string Code, string System, long Version, FaultContentRequest Content, string ApprovedBy, DateTimeOffset ApprovedAt);
