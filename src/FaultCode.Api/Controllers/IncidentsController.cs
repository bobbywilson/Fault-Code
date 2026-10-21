using FaultCode.Api.Authorization;
using FaultCode.Api.Contracts;
using FaultCode.Application.Incidents;
using FaultCode.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaultCode.Api.Controllers;

[Route("api/v1/incidents")]
public sealed class IncidentsController(IncidentService service, IAuthorizationService authorization) : FleetControllerBase
{
    private async Task<Incident> Read(Guid id, CancellationToken ct)
    {
        var incident = await service.Get(Caller, id, ct);
        if (!(await authorization.AuthorizeAsync(User, new DriverResource(incident.TenantId, incident.Fleet.OwnerObjectId), "vehicles.resource.read")).Succeeded)
            throw new IncidentException(404);
        return incident;
    }
    internal static IncidentResponse MapIncident(Incident i) => new(i.Id, i.SourceClientId, i.SourceEventId, i.Code, i.System, i.Model,
        i.SoftwareVersion, i.OccurredAt, i.ReceivedAt, i.IntakeCorrelationId,
        new(i.Fleet.VehicleId, i.Fleet.VehicleIdentifier, i.Fleet.VehicleVersion, i.Fleet.DriverId, i.Fleet.DriverIdentifier,
            i.Fleet.DriverVersion, i.Fleet.Association.Id, i.Fleet.Association.From, i.Fleet.Association.Until),
        i.Knowledge is not { } k ? null : new(k.DefinitionId, k.PublicationVersion, new(k.Content.Description, k.Content.ClassificationId,
            k.Content.ClassificationVersion, k.Content.Symptoms.ToArray(), k.Content.References.Select(r => new ReferenceRequest(r.Id, r.Version)).ToArray(),
            k.Content.Remedies.Select(r => new RemedyRequest(r.Id, r.Kind, r.Description, r.Approved, r.Model, r.SoftwareVersion,
                r.Patch is null ? null : new PatchRequest(r.Patch.Reference, r.Patch.SoftwareVersion))).ToArray()), k.ApprovedBy, k.ApprovedAt),
        i.Status, i.Version, i.Work.Select(w => new IncidentWorkResponse(w.Id, w.Kind, w.State, w.Attempts, w.FailureCode)).ToArray());
    [HttpPost(Name = "CreateIncident"), Authorize(Policy = "faults.write"), Authorize(Policy = "knowledge.read")]
    [ProducesResponseType<MutationResponse>(201)]
    public async Task<ActionResult<MutationResponse>> Create(CreateIncidentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Create(Caller, new(request.VehicleId, request.DriverId, request.SourceEventId, request.Code, request.System,
            request.Model, request.SoftwareVersion, request.OccurredAt), Key(key), ct), true, "/api/v1/incidents/");
    [HttpGet(Name = "ListIncidents"), Authorize(Policy = "faults.read"), Authorize(Policy = "vehicles.access.read"), Authorize(Policy = "knowledge.read")]
    [ProducesResponseType<PageResponse<IncidentResponse>>(200)]
    public async Task<ActionResult<PageResponse<IncidentResponse>>> List([FromQuery] IncidentQuery query, CancellationToken ct)
    {
        var page = await service.List(Caller, query.Page, query.PageSize, query.Status, query.VehicleId, ct);
        return Ok(new PageResponse<IncidentResponse>(page.Items.Select(MapIncident).ToArray(), page.Page, page.PageSize, page.TotalCount));
    }
    [HttpGet("{id:guid}", Name = "GetIncident"), Authorize(Policy = "faults.read"), Authorize(Policy = "vehicles.access.read"), Authorize(Policy = "knowledge.read")]
    [ProducesResponseType<IncidentResponse>(200)]
    public async Task<ActionResult<IncidentResponse>> Get(Guid id, CancellationToken ct)
    { var incident = await Read(id, ct); ETag(incident.Version); return Ok(MapIncident(incident)); }
    [HttpGet("{id:guid}/history", Name = "IncidentHistory"), Authorize(Policy = "faults.read"), Authorize(Policy = "vehicles.access.read"), Authorize(Policy = "knowledge.read")]
    [ProducesResponseType<PageResponse<IncidentHistoryResponse>>(200)]
    public async Task<ActionResult<PageResponse<IncidentHistoryResponse>>> History(Guid id, [FromQuery] FleetPageQuery query, CancellationToken ct)
    {
        var incident = await Read(id, ct); var offset = ((long)query.Page - 1) * query.PageSize;
        var items = offset >= incident.History.Length ? [] : incident.History.Skip((int)offset).Take(query.PageSize)
            .Select(h => new IncidentHistoryResponse(h.Version, h.Status, h.Action, h.ActorId, h.ClientId, h.CorrelationId, h.At, h.FailureCode)).ToArray();
        return Ok(new PageResponse<IncidentHistoryResponse>(items, query.Page, query.PageSize, incident.History.Length));
    }
    [HttpPut("{id:guid}/status", Name = "TransitionIncident"), Authorize(Policy = "faults.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Transition(Guid id, IncidentStatusRequest request,
        [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Transition(Caller, id, request.Status, Version(version), Key(key), ct), false, "");
    [HttpPost("{id:guid}/failures", Name = "FailIncidentWork"), Authorize(Policy = "faults.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Failure(Guid id, IncidentFailureRequest request,
        [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Fail(Caller, id, request.Code, Version(version), Key(key), ct), false, "");
    [HttpPost("{id:guid}/retries", Name = "RetryIncidentWork"), Authorize(Policy = "faults.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Retry(Guid id, [FromHeader(Name = "If-Match")] string version,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Retry(Caller, id, Version(version), Key(key), ct), false, "");
    [HttpPost("{id:guid}/escalations", Name = "EscalateIncident"), Authorize(Policy = "escalations.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Escalate(Guid id, [FromHeader(Name = "If-Match")] string version,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Escalate(Caller, id, Version(version), Key(key), ct), false, "");
}
