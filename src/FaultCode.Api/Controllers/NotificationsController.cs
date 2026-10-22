using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FaultCode.Api.Contracts;
using FaultCode.Api.Mcp;
using FaultCode.Application.Incidents;
using FaultCode.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FaultCode.Api.Controllers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateNotificationRequest(Guid IncidentId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateNotificationRequest([Required] string State);
[Route("api/v1/notifications")]
[Authorize(Policy = "faults.read"), Authorize(Policy = "vehicles.access.read"), Authorize(Policy = "drivers.access.read"), Authorize(Policy = "knowledge.read")]
public sealed class NotificationsController(NotificationService service, ResourceMutationService mutations, FaultEmailWorkflow workflow) : FleetControllerBase
{
    [HttpGet(Name = "ListDriverNotifications"), ProducesResponseType<PageResponse<DriverNotificationSummaryResponse>>(200)]
    public async Task<ActionResult<PageResponse<DriverNotificationSummaryResponse>>> List([FromQuery] FleetPageQuery query,
        [FromQuery] Guid? driverId, [FromQuery] Guid? incidentId, CancellationToken ct)
    {
        var p = await service.ListAll(Caller, driverId, query.Page, query.PageSize, ct, incidentId);
        return Ok(new PageResponse<DriverNotificationSummaryResponse>(p.Items.Select(DriverNotificationSummaryResponse.From).ToArray(), p.Page, p.PageSize, p.TotalCount));
    }
    [HttpGet("{id:guid}", Name = "GetNotification"), ProducesResponseType<NotificationResponse>(200)]
    public async Task<ActionResult<NotificationResponse>> Get(Guid id, CancellationToken ct)
    { var n = await service.Get(Caller, id, ct); ETag(n.Version); return Ok(NotificationResponse.From(n)); }
    [HttpPost(Name = "CreateNotification"), Authorize(Policy = "faults.write"), ProducesResponseType<MutationResponse>(201)]
    public async Task<ActionResult<MutationResponse>> Create(CreateNotificationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        var result = await mutations.CreateNotification(Caller, request.IncidentId, Key(key), ct);
        await Process(result.Id, ct);
        var current = await service.Get(Caller, result.Id, ct);
        return Mutation(new(result.Id, current.Version), true, "/api/v1/notifications/");
    }
    [HttpPut("{id:guid}", Name = "UpdateNotification"), Authorize(Policy = "faults.write"), ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Update(Guid id, UpdateNotificationRequest request,
        [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        var result = await mutations.UpdateNotification(Caller, id, request.State, Version(version), Key(key), ct);
        if (request.State == "AwaitingAgent") await Process(id, ct);
        var current = await service.Get(Caller, id, ct);
        return Mutation(new(id, current.Version), false, "");
    }
    [HttpDelete("{id:guid}", Name = "DeleteNotification"), Authorize(Policy = "faults.write"), ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Delete(Guid id, [FromHeader(Name = "If-Match")] string version,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await mutations.DeleteNotification(Caller, id, Version(version), Key(key), ct), false, "");
    private async Task Process(Guid notification, CancellationToken ct)
    {
        try { await workflow.Process(Caller, notification, ct); }
        catch (Exception) when (!ct.IsCancellationRequested) { /* Actual delivery remains visible in GET notification. */ }
    }
}
