using FaultCode.Api.Authorization;
using FaultCode.Api.Contracts;
using FaultCode.Application.Knowledge;
using FaultCode.Application.Ontology;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaultCode.Api.Controllers;

[Route("api/v1/ontology")]
public sealed class OntologyController(OntologyService service) : FleetControllerBase
{
    [HttpGet("relationships", Name = "ListOntologyRelationships"), Authorize(Policy = "knowledge.read")]
    [ProducesResponseType<DomainOntologyResponse>(200)]
    public ActionResult<DomainOntologyResponse> Relationships() => Ok(new DomainOntologyResponse(DomainOntology.Version, DomainOntology.Relationships));

    private KnowledgeCaller Identity => new(PermissionHandler.Identifier(User, "tid") ?? "", PermissionHandler.Identifier(User, "oid") ?? "",
        PermissionHandler.Identifier(User, "azp") ?? "", HttpContext.TraceIdentifier);
    [HttpGet("draft", Name = "GetOntologyDraft"), Authorize(Policy = "knowledge.publish")]
    [ProducesResponseType<OntologyDraftResponse>(200)]
    public async Task<ActionResult<OntologyDraftResponse>> Draft(CancellationToken ct)
    { var catalog = await service.Draft(Identity, ct); ETag(catalog.Version); return Ok(new OntologyDraftResponse(catalog.Id, catalog.Version, OntologyResponses.Content(catalog.Draft))); }
    [HttpPost("draft", Name = "CreateOntologyDraft"), Authorize(Policy = "knowledge.publish")]
    [ProducesResponseType<MutationResponse>(201)]
    public async Task<ActionResult<MutationResponse>> Create(OntologyContentRequest body, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        var result = await service.Create(Identity, body.ToDomain(), Key(key), ct); ETag(result.Version);
        return Created("/api/v1/ontology/draft", new MutationResponse(result.Id, result.Version));
    }
    [HttpPut("draft", Name = "UpdateOntologyDraft"), Authorize(Policy = "knowledge.publish")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Update(OntologyContentRequest body, [FromHeader(Name = "If-Match")] string version, CancellationToken ct)
    { var result = await service.Update(Identity, body.ToDomain(), Version(version), ct); ETag(result.Version); return Ok(new MutationResponse(result.Id, result.Version)); }
    [HttpGet("history", Name = "OntologyHistory"), Authorize(Policy = "knowledge.publish")]
    [ProducesResponseType<PageResponse<OntologyRevisionResponse>>(200)]
    public async Task<ActionResult<PageResponse<OntologyRevisionResponse>>> History([FromQuery] FleetPageQuery query, CancellationToken ct)
    {
        var catalog = await service.Draft(Identity, ct); var offset = ((long)query.Page - 1) * query.PageSize;
        var items = offset >= catalog.History.Length ? [] : catalog.History.Skip((int)offset).Take(query.PageSize).Select(OntologyResponses.Revision).ToArray();
        return Ok(new PageResponse<OntologyRevisionResponse>(items, query.Page, query.PageSize, catalog.History.Length));
    }
    [HttpPost("publications", Name = "PublishOntology"), Authorize(Policy = "knowledge.publish")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Publish([FromHeader(Name = "If-Match")] string version,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    { var result = await service.Publish(Identity, Version(version), Key(key), ct); ETag(result.Version); return Ok(new MutationResponse(result.Id, result.Version)); }
    [HttpGet("publications/{version:long}", Name = "GetApprovedOntology"), Authorize(Policy = "knowledge.read")]
    [ProducesResponseType<OntologyPublicationResponse>(200)]
    public async Task<ActionResult<OntologyPublicationResponse>> Published(long version, CancellationToken ct)
    { var result = await service.Published(Identity, version, ct); ETag(result.Version); return Ok(OntologyResponses.Publication(result)); }
    [HttpPost("evaluations", Name = "EvaluateOntology"), Authorize(Policy = "knowledge.read")]
    [ProducesResponseType<OntologyEvaluationResponse>(201)]
    public async Task<ActionResult<OntologyEvaluationResponse>> Evaluate(EvaluateOntologyRequest body, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        var result = await service.Evaluate(Identity, body.ToDomain(), Key(key), ct);
        ETag(result.PublicationVersion); return Created("/api/v1/ontology/evaluations/" + result.Id, OntologyResponses.Evaluation(result));
    }
    [HttpGet("evaluations/{id:guid}", Name = "GetOntologyEvaluation"), Authorize(Policy = "knowledge.read")]
    [ProducesResponseType<OntologyEvaluationResponse>(200)]
    public async Task<ActionResult<OntologyEvaluationResponse>> Evaluation(Guid id, CancellationToken ct)
    { var result = await service.Evaluation(Identity, id, ct); ETag(result.PublicationVersion); return Ok(OntologyResponses.Evaluation(result)); }
}

public sealed record DomainOntologyResponse(string Version, IReadOnlyList<DomainRelationship> Items);
