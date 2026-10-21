using FaultCode.Api.Contracts;
using FaultCode.Application.Escalations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaultCode.Api.Controllers;

[Route("api/v1/engineering-escalations")]
[Authorize(Policy = "faults.read"), Authorize(Policy = "vehicles.access.read"), Authorize(Policy = "knowledge.read")]
public sealed class EngineeringCasesController(EscalationService service) : FleetControllerBase
{
    [HttpGet(Name = "ListEngineeringCases"), Authorize(Policy = "escalations.read")]
    [ProducesResponseType<PageResponse<EngineeringCaseResponse>>(200)]
    public async Task<ActionResult<PageResponse<EngineeringCaseResponse>>> List([FromQuery] EngineeringQuery query, CancellationToken ct)
    {
        var p = await service.List(Caller, query.Page, query.PageSize, query.Status, ct);
        return Ok(new PageResponse<EngineeringCaseResponse>(p.Items.Select(EngineeringCaseResponse.From).ToArray(), p.Page, p.PageSize, p.TotalCount));
    }
    [HttpGet("{id:guid}", Name = "GetEngineeringCase"), Authorize(Policy = "escalations.read")]
    [ProducesResponseType<EngineeringCaseResponse>(200)]
    public async Task<ActionResult<EngineeringCaseResponse>> Get(Guid id, CancellationToken ct)
    { var e = await service.Get(Caller, id, ct); ETag(e.Version); return Ok(EngineeringCaseResponse.From(e)); }
    [HttpGet("{id:guid}/history", Name = "EngineeringCaseHistory"), Authorize(Policy = "escalations.read")]
    [ProducesResponseType<PageResponse<EngineeringHistoryResponse>>(200)]
    public async Task<ActionResult<PageResponse<EngineeringHistoryResponse>>> History(Guid id, [FromQuery] FleetPageQuery query, CancellationToken ct)
    {
        var p = await service.History(Caller, id, query.Page, query.PageSize, ct);
        return Ok(new PageResponse<EngineeringHistoryResponse>(p.Items.Select(EngineeringHistoryResponse.From).ToArray(), p.Page, p.PageSize, p.TotalCount));
    }
    [HttpGet("{id:guid}/events", Name = "EngineeringCaseEvents"), Authorize(Policy = "escalations.read")]
    [ProducesResponseType<PageResponse<EngineeringEventResponse>>(200)]
    public async Task<ActionResult<PageResponse<EngineeringEventResponse>>> Events(Guid id, [FromQuery] FleetPageQuery query, CancellationToken ct)
    {
        var p = await service.Events(Caller, id, query.Page, query.PageSize, ct);
        return Ok(new PageResponse<EngineeringEventResponse>(p.Items.Select(EngineeringEventResponse.From).ToArray(), p.Page, p.PageSize, p.TotalCount));
    }
    [HttpPost(Name = "CreateEngineeringCase"), Authorize(Policy = "escalations.write")]
    [ProducesResponseType<MutationResponse>(201)]
    public async Task<ActionResult<MutationResponse>> Create(CreateEngineeringCaseRequest request,
        [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Create(Caller, request.IncidentId, request.InvestigationId, Version(version), Key(key), ct), true, "/api/v1/engineering-escalations/");
    [HttpPut("{id:guid}/assignment", Name = "AssignEngineeringCase"), Authorize(Policy = "escalations.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Assign(Guid id, EngineeringAssignmentRequest request,
        [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Assign(Caller, id, Version(version), Key(key), request.AssigneeId, ct), false, "");
    [HttpPost("{id:guid}/notes", Name = "AddEngineeringNote"), Authorize(Policy = "escalations.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Note(Guid id, EngineeringNoteRequest request,
        [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Note(Caller, id, Version(version), Key(key), request.Text, ct), false, "");
    [HttpPut("{id:guid}/status", Name = "ChangeEngineeringStatus"), Authorize(Policy = "escalations.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Status(Guid id, EngineeringStatusRequest request,
        [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Status(Caller, id, Version(version), Key(key), request.Status, ct), false, "");
    [HttpPost("{id:guid}/resolutions", Name = "ProposeEngineeringResolution"), Authorize(Policy = "escalations.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Propose(Guid id, EngineeringResolutionRequest request,
        [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Propose(Caller, id, Version(version), Key(key), request.Content.ToDomain(), request.ExpectedFaultVersion, ct), false, "");
    [HttpPost("{id:guid}/approvals", Name = "ApproveEngineeringResolution"), Authorize(Policy = "escalations.write"), Authorize(Policy = "knowledge.publish")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Approve(Guid id, EngineeringApprovalRequest request,
        [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Approve(Caller, id, Version(version), Key(key), ct), false, "");
}
