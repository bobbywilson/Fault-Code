using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FaultCode.Domain;
using FaultCode.Application.Retrieval;
namespace FaultCode.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DocumentPageRequest(int Number, [Required] string Text);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DocumentContentRequest([Required] string Title, [Required] string SourceVersion, [Required] string System,
    [Required] string Model, [Required] string SoftwareVersion, [Required] string Access, [Required] DocumentPageRequest[] Pages)
{
    public DocumentContent ToDomain() => new(Title, SourceVersion, System, Model, SoftwareVersion, Access,
        [.. Pages.Select(p => p is null ? null! : new DocumentPage(p.Number, p.Text))]);
    public static DocumentContentRequest From(DocumentContent c) => new(c.Title, c.SourceVersion, c.System, c.Model, c.SoftwareVersion, c.Access,
        c.Pages.Select(p => new DocumentPageRequest(p.Number, p.Text)).ToArray());
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateDocumentRequest([Required] string SourceId, [Required] DocumentContentRequest Content);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SearchEvidenceRequest([Required] string Text, [Required] string System, [Required] string Model, [Required] string SoftwareVersion, int Limit = 5);
public sealed record DocumentPublicationResponse(long Version, DocumentContentRequest Content, string Checksum, string ApprovedBy, DateTimeOffset ApprovedAt);
public sealed record DocumentResponse(Guid Id, string SourceId, long Version, DocumentContentRequest Draft,
    DocumentPublicationResponse? Publication, string? IndexedProfile);
public sealed record EvidenceCitationResponse(Guid ChunkId, Guid DocumentId, string SourceId, string SourceVersion, long PublicationVersion,
    int Page, int Offset, string Checksum, string DocumentChecksum, string Text, double Score, string ApprovedBy, DateTimeOffset ApprovedAt);
public sealed record EvidenceResponse(string Status, string EvidencePolicy, EvidenceCitationResponse[] Citations)
{
    public static EvidenceResponse From(EvidenceResult r) => new(r.Status, r.EvidencePolicy, r.Citations.Select(c => new EvidenceCitationResponse(c.ChunkId,
        c.DocumentId, c.SourceId, c.SourceVersion, c.PublicationVersion, c.Page, c.Offset, c.Checksum, c.DocumentChecksum, c.Text, c.Score, c.ApprovedBy, c.ApprovedAt)).ToArray());
}

public sealed record ReindexResponse(Guid Id, int IndexedDocuments);
