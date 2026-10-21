using FaultCode.Api.Authorization;
using FaultCode.Api.Contracts;
using FaultCode.Application.Knowledge;
using FaultCode.Application.Retrieval;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FaultCode.Api.Controllers;

[Route("api/v1/knowledge")]
public sealed class DocumentsController(RetrievalService service) : FleetControllerBase
{
    private KnowledgeCaller Identity => new(PermissionHandler.Identifier(User, "tid") ?? "", PermissionHandler.Identifier(User, "oid") ?? "",
        PermissionHandler.Identifier(User, "azp") ?? "", HttpContext.TraceIdentifier);
    [HttpPost("documents", Name = "CreateDocument"), Authorize(Policy = "knowledge.publish")]
    [ProducesResponseType<MutationResponse>(201)]
    public async Task<ActionResult<MutationResponse>> Create(CreateDocumentRequest body, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Create(Identity, new(body.SourceId, body.Content.ToDomain()), Key(key), ct), true, "/api/v1/knowledge/documents/");
    [HttpGet("documents/{id:guid}", Name = "GetDocument"), Authorize(Policy = "knowledge.publish")]
    [ProducesResponseType<DocumentResponse>(200)]
    public async Task<ActionResult<DocumentResponse>> Get(Guid id, CancellationToken ct)
    {
        var d = await service.Get(Identity, id, ct); ETag(d.Version);
        var p = d.Publications.LastOrDefault();
        return Ok(new DocumentResponse(d.Id, d.SourceId, d.Version, DocumentContentRequest.From(d.Draft),
            p is null ? null : new DocumentPublicationResponse(p.Version, DocumentContentRequest.From(p.Content), p.Checksum, p.ApprovedBy, p.ApprovedAt), d.IndexedProfile));
    }
    [HttpPut("documents/{id:guid}", Name = "UpdateDocument"), Authorize(Policy = "knowledge.publish")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Update(Guid id, DocumentContentRequest body, [FromHeader(Name = "If-Match")] string version, CancellationToken ct) =>
        Mutation(await service.Update(Identity, id, body.ToDomain(), Version(version), ct), false, "");
    [HttpPost("documents/{id:guid}/publications", Name = "PublishDocument"), Authorize(Policy = "knowledge.publish")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Publish(Guid id, [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Publish(Identity, id, Version(version), Key(key), ct), false, "");
    [HttpDelete("documents/{id:guid}", Name = "DeleteDocument"), Authorize(Policy = "knowledge.publish")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Delete(Guid id, [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Delete(Identity, id, Version(version), Key(key), ct), false, "");
    [HttpPost("documents/{id:guid}/index", Name = "IndexDocument"), Authorize(Policy = "knowledge.publish")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Index(Guid id, [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Index(Identity, id, Version(version), Key(key), ct), false, "");
    [HttpPost("reindex", Name = "ReindexDocuments"), Authorize(Policy = "knowledge.publish")]
    [ProducesResponseType<ReindexResponse>(200)]
    public async Task<ActionResult<ReindexResponse>> Reindex([FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        var result = await service.Reindex(Identity, Key(key), ct);
        return Ok(new ReindexResponse(result.Id, checked((int)result.Version)));
    }
    [HttpPost("search", Name = "SearchEvidence"), Authorize(Policy = "knowledge.read")]
    [ProducesResponseType<EvidenceResponse>(200)]
    public async Task<ActionResult<EvidenceResponse>> Search(SearchEvidenceRequest body, CancellationToken ct) =>
        Ok(EvidenceResponse.From(await service.Search(Identity, new(body.Text, body.System, body.Model, body.SoftwareVersion, body.Limit), ct)));
}
