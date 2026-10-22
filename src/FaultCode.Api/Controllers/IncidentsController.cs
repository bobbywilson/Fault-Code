using FaultCode.Api.Authorization;
using FaultCode.Api.Contracts;
using FaultCode.Api.Mcp;
using FaultCode.Application.Incidents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FaultCode.Api.Controllers;

[Route("api/v1/incidents")]
[Authorize(Policy = "faults.read"), Authorize(Policy = "vehicles.access.read"), Authorize(Policy = "drivers.access.read"), Authorize(Policy = "knowledge.read")]
public sealed class IncidentsController(IncidentService service, VehicleFaultDispatchService dispatch, ResourceMutationService mutations, FaultEmailWorkflow workflow, IncidentTrackingService tracking) : FleetControllerBase
{
    internal static IncidentResponse MapIncident(FaultCode.Domain.Incident i) => new(i.Id, i.SourceClientId, i.SourceEventId, i.Code, i.System, i.Model,
        i.SoftwareVersion, i.OccurredAt, i.ReceivedAt, i.IntakeCorrelationId,
        new(i.Fleet.VehicleId, i.Fleet.VehicleIdentifier, i.Fleet.VehicleVersion, i.Fleet.DriverId, i.Fleet.DriverIdentifier,
            i.Fleet.DriverVersion, i.Fleet.Association.Id, i.Fleet.Association.From, i.Fleet.Association.Until),
        i.Knowledge is not { } k ? null : new(k.DefinitionId, k.PublicationVersion, new(k.Content.Description, k.Content.ClassificationId,
            k.Content.ClassificationVersion, k.Content.Symptoms.ToArray(), k.Content.References.Select(r => new ReferenceRequest(r.Id, r.Version)).ToArray(),
            k.Content.Remedies.Select(r => new RemedyRequest(r.Id, r.Kind, r.Description, r.Approved, r.Model, r.SoftwareVersion,
                r.Patch is null ? null : new PatchRequest(r.Patch.Reference, r.Patch.SoftwareVersion))).ToArray()), k.ApprovedBy, k.ApprovedAt),
        i.Status, i.Version, i.Work.Select(w => new IncidentWorkResponse(w.Id, w.Kind, w.State, w.Attempts, w.FailureCode)).ToArray())
    {
        Resolution = i.Resolution is { } r ? new(r.Text, r.ActorId, r.ClientId, r.At, r.Version) : null,
        Datadog = i.Tracking is { } t ? new(t.State, t.Id, t.Number, t.Url, t.Version, t.FailureCode) : null
    };
    [HttpGet(Name = "ListIncidents"), ProducesResponseType<PageResponse<IncidentResponse>>(200)]
    public async Task<ActionResult<PageResponse<IncidentResponse>>> List([FromQuery] IncidentQuery query, CancellationToken ct)
    {
        var p = await service.List(Caller, query.Page, query.PageSize, query.Status, query.VehicleId, ct, query.UnmatchedOnly);
        return Ok(new PageResponse<IncidentResponse>(p.Items.Select(MapIncident).ToArray(), p.Page, p.PageSize, p.TotalCount));
    }
    [HttpGet("{id:guid}", Name = "GetIncident"), ProducesResponseType<IncidentResponse>(200)]
    public async Task<ActionResult<IncidentResponse>> Get(Guid id, CancellationToken ct)
    { var i = await service.Get(Caller, id, ct); ETag(i.Version); return Ok(MapIncident(i)); }
    [HttpPost(Name = "CreateIncident"), Authorize(Policy = "faults.write"), ProducesResponseType<FaultDispatchReceipt>(201)]
    public async Task<ActionResult<FaultDispatchReceipt>> Create(DispatchVehicleFaultRequest request,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        var receipt = await dispatch.Dispatch(Caller, request.VehicleId, request.Code, Key(key), ct);
        await tracking.Sync(Caller.TenantId, receipt.IncidentId, ct);
        await Process(receipt.NotificationId, ct);
        return Created($"/api/v1/incidents/{receipt.IncidentId:D}", receipt);
    }
    [HttpPut("{id:guid}", Name = "UpdateIncident"), Authorize(Policy = "faults.write"), ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Update(Guid id, IncidentStatusRequest request,
        [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        var result = await mutations.UpdateIncident(Caller, id, request.Status, Version(version), Key(key), ct, request.Resolution);
        await tracking.Sync(Caller.TenantId, id, ct);
        if (result.NotificationId is { } notification) await Process(notification, ct);
        return Mutation(result.Mutation, false, "");
    }
    [HttpDelete("{id:guid}", Name = "DeleteIncident"), Authorize(Policy = "faults.write"), ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Delete(Guid id, [FromHeader(Name = "If-Match")] string version,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await mutations.DeleteIncident(Caller, id, Version(version), Key(key), ct), false, "");
    private async Task Process(Guid notification, CancellationToken ct)
    {
        try { await workflow.Process(Caller, notification, ct); }
        catch (Exception) when (!ct.IsCancellationRequested) { /* Durable state records failed/pending delivery; receipt does not claim sending. */ }
    }
}
