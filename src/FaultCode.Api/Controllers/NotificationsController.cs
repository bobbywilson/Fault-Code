using FaultCode.Api.Contracts;
using FaultCode.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaultCode.Api.Controllers;

[Route("api/v1")]
[Authorize(Policy = "faults.read"), Authorize(Policy = "vehicles.access.read"), Authorize(Policy = "drivers.access.read"), Authorize(Policy = "knowledge.read")]
public sealed class NotificationsController(NotificationService service) : FleetControllerBase
{
    [HttpGet("incidents/{id:guid}/notifications", Name = "ListIncidentNotifications")]
    [ProducesResponseType<PageResponse<NotificationResponse>>(200)]
    public async Task<ActionResult<PageResponse<NotificationResponse>>> List(Guid id, [FromQuery] FleetPageQuery query, CancellationToken ct)
    {
        var p = await service.List(Caller, id, query.Page, query.PageSize, ct);
        return Ok(new PageResponse<NotificationResponse>(p.Items.Select(NotificationResponse.From).ToArray(), p.Page, p.PageSize, p.TotalCount));
    }
    [HttpPost("incidents/{id:guid}/notifications", Name = "EnqueueIncidentNotification"), Authorize(Policy = "faults.write")]
    [ProducesResponseType<MutationResponse>(201)]
    public async Task<ActionResult<MutationResponse>> Enqueue(Guid id, EnqueueNotificationRequest request,
        [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Enqueue(Caller, id, request.SourceKind, request.SourceId, Version(version), Key(key), ct), true, "/api/v1/notifications/");
    [HttpGet("notifications/{id:guid}", Name = "GetNotification")]
    [ProducesResponseType<NotificationResponse>(200)]
    public async Task<ActionResult<NotificationResponse>> Get(Guid id, CancellationToken ct)
    { var n = await service.Get(Caller, id, ct); ETag(n.Version); return Ok(NotificationResponse.From(n)); }
    [HttpGet("notifications/{id:guid}/history", Name = "NotificationHistory")]
    [ProducesResponseType<PageResponse<NotificationHistoryResponse>>(200)]
    public async Task<ActionResult<PageResponse<NotificationHistoryResponse>>> History(Guid id, [FromQuery] FleetPageQuery query, CancellationToken ct)
    {
        var p = await service.History(Caller, id, query.Page, query.PageSize, ct);
        return Ok(new PageResponse<NotificationHistoryResponse>(p.Items.Select(NotificationHistoryResponse.From).ToArray(), p.Page, p.PageSize, p.TotalCount));
    }
    [HttpPost("notifications/{id:guid}/dispatches", Name = "DispatchNotification"), Authorize(Policy = "faults.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Dispatch(Guid id, NotificationActionRequest request,
        [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Dispatch(Caller, id, Version(version), Key(key), ct), false, "");
    [HttpPost("notifications/{id:guid}/cancellations", Name = "CancelNotification"), Authorize(Policy = "faults.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Cancel(Guid id, NotificationActionRequest request,
        [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Cancel(Caller, id, Version(version), Key(key), ct), false, "");
}
[Route("api/v1/drivers/{id:guid}/notification-preferences")]
[Authorize(Policy = "drivers.access.read")]
public sealed class NotificationPreferencesController(NotificationService service) : FleetControllerBase
{
    [HttpGet(Name = "GetNotificationPreferences")]
    [ProducesResponseType<LocalNotificationPreferenceResponse>(200)]
    public async Task<ActionResult<LocalNotificationPreferenceResponse>> Get(Guid id, CancellationToken ct)
    { var p = await service.Preference(Caller, id, ct); ETag(p.DriverVersion); return Ok(LocalNotificationPreferenceResponse.From(p)); }
    [HttpPut(Name = "UpdateNotificationPreferences"), Authorize(Policy = "drivers.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Put(Guid id, LocalNotificationPreferenceRequest request,
        [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await service.Preferences(Caller, id, Version(version), Key(key), request.Enabled, request.Contact, ct), false, "");
}
