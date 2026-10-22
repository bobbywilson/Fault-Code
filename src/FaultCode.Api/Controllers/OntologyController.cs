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
    private KnowledgeCaller Identity => new(PermissionHandler.Identifier(User, "tid") ?? "", PermissionHandler.Identifier(User, "oid") ?? "",
        PermissionHandler.Identifier(User, "azp") ?? "", HttpContext.TraceIdentifier);
    [HttpGet(Name = "ListOntology"), Authorize(Policy = "knowledge.read"), ProducesResponseType<PageResponse<OntologyDraftResponse>>(200)]
    public async Task<ActionResult<PageResponse<OntologyDraftResponse>>> List([FromQuery] FleetPageQuery query, CancellationToken ct)
    {
        OntologyDraftResponse[] rows;
        try { var c = await service.Draft(Identity, ct); rows = [new(c.Id, c.Version, OntologyResponses.Content(c.Draft))]; }
        catch (KnowledgeException e) when (e.Status == 404) { rows = []; }
        return Ok(new PageResponse<OntologyDraftResponse>(query.Page == 1 ? rows : [], query.Page, query.PageSize, rows.Length));
    }
    [HttpGet("{id:guid}", Name = "GetOntology"), Authorize(Policy = "knowledge.read"), ProducesResponseType<OntologyDraftResponse>(200)]
    public async Task<ActionResult<OntologyDraftResponse>> Get(Guid id, CancellationToken ct)
    { var c = await service.Draft(Identity, ct); if (c.Id != id) return NotFound(); ETag(c.Version); return Ok(new OntologyDraftResponse(c.Id, c.Version, OntologyResponses.Content(c.Draft))); }
    [HttpPost(Name = "CreateOntology"), Authorize(Policy = "knowledge.publish"), ProducesResponseType<MutationResponse>(201)]
    public async Task<ActionResult<MutationResponse>> Create(OntologyContentRequest request, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    { var r = await service.Create(Identity, request.ToDomain(), Key(key), ct); return Mutation(new(r.Id, r.Version), true, "/api/v1/ontology/"); }
    [HttpPut("{id:guid}", Name = "UpdateOntology"), Authorize(Policy = "knowledge.publish"), ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Update(Guid id, OntologyContentRequest request, [FromHeader(Name = "If-Match")] string version, CancellationToken ct)
    {
        var c = await service.Draft(Identity, ct); if (c.Id != id) return NotFound();
        var r = await service.Update(Identity, request.ToDomain(), Version(version), ct); return Mutation(new(r.Id, r.Version), false, "");
    }
    [HttpDelete("{id:guid}", Name = "DeleteOntology"), Authorize(Policy = "knowledge.publish"), ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Delete(Guid id, [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    { var r = await service.Delete(Identity, id, Version(version), Key(key), ct); return Mutation(new(r.Id, r.Version), false, ""); }
}
