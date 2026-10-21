using FaultCode.Api.Contracts;
using FaultCode.Api.Authorization;
using FaultCode.Application.Fleet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaultCode.Api.Controllers;

[Route("api/v1/drivers")]
public sealed class DriversController(FleetService fleet, IAuthorizationService authorization) : FleetControllerBase
{
    [HttpGet(Name = "ListDrivers")]
    [Authorize(Policy = "drivers.access.read")]
    [ProducesResponseType<PageResponse<DriverResponse>>(200)]
    public async Task<ActionResult<PageResponse<DriverResponse>>> List([FromQuery] FleetQuery query, CancellationToken ct)
    {
        var page = await fleet.Drivers(Caller, query.Page, query.PageSize, query.Active, ct);
        return Ok(new PageResponse<DriverResponse>(page.Items.Select(Map).ToArray(), page.Page, page.PageSize, page.TotalCount));
    }
    [HttpGet("{id:guid}", Name = "GetDriver")]
    [Authorize(Policy = "drivers.access.read")]
    [ProducesResponseType<DriverResponse>(200)]
    public async Task<ActionResult<DriverResponse>> Get(Guid id, CancellationToken ct)
    {
        var d = await fleet.GetDriver(Caller, id, ct);
        if (!(await authorization.AuthorizeAsync(User, new DriverResource(d.TenantId, d.OwnerObjectId), "drivers.resource.read")).Succeeded) return Forbid();
        ETag(d.Version); return Ok(Map(d));
    }
    [HttpPost(Name = "CreateDriver")]
    [Authorize(Policy = "drivers.write")]
    [ProducesResponseType<MutationResponse>(201)]
    public async Task<ActionResult<MutationResponse>> Create(CreateDriverRequest request,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await fleet.CreateDriver(Caller, new(request.Identifier, request.OwnerObjectId, request.DisplayName, request.Email, request.EmailNotifications), Key(key), ct), true, "/api/v1/drivers/");
    [HttpPut("{id:guid}", Name = "UpdateDriver")]
    [Authorize(Policy = "drivers.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Update(Guid id, UpdateDriverRequest request,
        [FromHeader(Name = "If-Match")] string version, CancellationToken ct) =>
        Mutation(await fleet.UpdateDriver(Caller, id, new(request.DisplayName, request.Email, request.EmailNotifications), Version(version), ct), false, "");
    [HttpDelete("{id:guid}", Name = "DeactivateDriver")]
    [Authorize(Policy = "drivers.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Deactivate(Guid id, [FromHeader(Name = "If-Match")] string version, CancellationToken ct) =>
        Mutation(await fleet.Deactivate(Caller, id, true, Version(version), ct), false, "");
}
