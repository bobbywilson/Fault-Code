using System.ComponentModel.DataAnnotations;
using FaultCode.Api.Authorization;
using FaultCode.Api.Contracts;
using FaultCode.Application.Knowledge;
using FaultCode.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaultCode.Api.Controllers;

[Route("api/v1/faults")]
public sealed class FaultsController(KnowledgeService service) : FleetControllerBase
{
    private KnowledgeCaller Identity => new(PermissionHandler.Identifier(User, "tid") ?? "", PermissionHandler.Identifier(User, "oid") ?? "",
        PermissionHandler.Identifier(User, "azp") ?? "", HttpContext.TraceIdentifier);
    private static FaultContentRequest Content(FaultContent c) => new(c.Description, c.ClassificationId, c.ClassificationVersion,
        c.Symptoms.ToArray(), c.References.Select(x => new ReferenceRequest(x.Id, x.Version)).ToArray(),
        c.Remedies.Select(x => new RemedyRequest(x.Id, x.Kind, x.Description, x.Approved, x.Model, x.SoftwareVersion,
            x.Patch is null ? null : new PatchRequest(x.Patch.Reference, x.Patch.SoftwareVersion))).ToArray());
    private static FaultResponse MapFault(FaultDefinition f) => new(f.Id, f.Code, f.System, f.Retired, f.Version, Content(f.Draft));
    [HttpGet(Name = "ListFaults"), Authorize(Policy = "faults.read")]
    [ProducesResponseType<PageResponse<FaultResponse>>(200)]
    public async Task<ActionResult<PageResponse<FaultResponse>>> List([FromQuery] FaultQuery query, CancellationToken ct)
    {
        var page = await service.List(Identity, query.Page, query.PageSize, query.System, query.Search, query.Retired, ct);
        return Ok(new PageResponse<FaultResponse>(page.Items.Select(MapFault).ToArray(), page.Page, page.PageSize, page.TotalCount));
    }
    [HttpGet("{id:guid}", Name = "GetFault"), Authorize(Policy = "faults.read")]
    [ProducesResponseType<FaultResponse>(200)]
    public async Task<ActionResult<FaultResponse>> Get(Guid id, CancellationToken ct)
    { var fault = await service.Get(Identity, id, ct); ETag(fault.Version); return Ok(MapFault(fault)); }
    [HttpGet("{id:guid}/history", Name = "FaultHistory"), Authorize(Policy = "faults.read")]
    [ProducesResponseType<PageResponse<FaultRevisionResponse>>(200)]
    public async Task<ActionResult<PageResponse<FaultRevisionResponse>>> History(Guid id, [FromQuery] FleetPageQuery query, CancellationToken ct)
    {
        var fault = await service.Get(Identity, id, ct); var offset = ((long)query.Page - 1) * query.PageSize;
        var items = offset >= fault.History.Length ? [] : fault.History.Skip((int)offset).Take(query.PageSize)
            .Select(h => new FaultRevisionResponse(h.Version, Content(h.Content), h.State, h.ActorId, h.At)).ToArray();
        return Ok(new PageResponse<FaultRevisionResponse>(items, query.Page, query.PageSize, fault.History.Length));
    }
    [HttpPost(Name = "CreateFault"), Authorize(Policy = "faults.write")]
    [ProducesResponseType<MutationResponse>(201)]
    public async Task<ActionResult<MutationResponse>> Create(CreateFaultRequest request, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Create(Identity, new(request.Code, request.System, request.Content.ToDomain()), Key(key), ct), true, "/api/v1/faults/");
    [HttpPut("{id:guid}", Name = "UpdateFault"), Authorize(Policy = "faults.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Update(Guid id, FaultContentRequest request, [FromHeader(Name = "If-Match")] string version, CancellationToken ct) =>
        Mutation(await service.Update(Identity, id, request.ToDomain(), Version(version), ct), false, "");
    [HttpDelete("{id:guid}", Name = "RetireFault"), Authorize(Policy = "faults.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Retire(Guid id, [FromHeader(Name = "If-Match")] string version, CancellationToken ct) =>
        Mutation(await service.Retire(Identity, id, Version(version), ct), false, "");
    [HttpPost("{id:guid}/publications", Name = "PublishFault"), Authorize(Policy = "knowledge.publish")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Publish(Guid id, [FromHeader(Name = "If-Match")] string version,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Publish(Identity, id, Version(version), Key(key), ct), false, "");
    [HttpGet("lookup", Name = "LookupApprovedFault"), Authorize(Policy = "knowledge.read")]
    [ProducesResponseType<ApprovedFaultResponse>(200)]
    public async Task<ActionResult<ApprovedFaultResponse>> Lookup([FromQuery, Required] string code, [FromQuery, Required] string system,
        [FromQuery, Required] string model, [FromQuery, Required] string software, CancellationToken ct)
    {
        var f = await service.Lookup(Identity, code, system, model, software, ct); ETag(f.Version);
        return Ok(new ApprovedFaultResponse(f.Id, f.Code, f.System, f.Version, Content(f.Content), f.ApprovedBy, f.ApprovedAt));
    }
}
