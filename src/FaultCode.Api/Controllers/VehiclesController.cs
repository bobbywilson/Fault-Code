using FaultCode.Api.Contracts;
using FaultCode.Api.Authorization;
using FaultCode.Application.Fleet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaultCode.Api.Controllers;

[Route("api/v1/vehicles")]
public sealed class VehiclesController(FleetService fleet, IAuthorizationService authorization, IFleetStore store) : FleetControllerBase
{
    [HttpGet(Name = "ListVehicles")]
    [Authorize(Policy = "vehicles.access.read")]
    [ProducesResponseType<PageResponse<VehicleResponse>>(200)]
    public async Task<ActionResult<PageResponse<VehicleResponse>>> List([FromQuery] FleetQuery query, CancellationToken ct)
    {
        var page = await fleet.Vehicles(Caller, query.Page, query.PageSize, query.Active, ct);
        return Ok(new PageResponse<VehicleResponse>((await Enrich(page.Items, ct)).ToArray(), page.Page, page.PageSize, page.TotalCount));
    }
    [HttpGet("{id:guid}", Name = "GetVehicle")]
    [Authorize(Policy = "vehicles.access.read")]
    [ProducesResponseType<VehicleResponse>(200)]
    public async Task<ActionResult<VehicleResponse>> Get(Guid id, CancellationToken ct)
    {
        var (v, d) = await fleet.GetVehicle(Caller, id, ct);
        if (d is not null && !(await authorization.AuthorizeAsync(User, new DriverResource(d.TenantId, d.OwnerObjectId), "vehicles.resource.read")).Succeeded) return Forbid();
        ETag(v.Version); return Ok((await Enrich([v], ct))[0]);
    }
    [HttpPost(Name = "CreateVehicle")]
    [Authorize(Policy = "vehicles.write")]
    [ProducesResponseType<MutationResponse>(201)]
    public async Task<ActionResult<MutationResponse>> Create(VehicleRequest request,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await fleet.CreateVehicle(Caller, new(request.Identifier, request.System), Key(key), ct), true, "/api/v1/vehicles/");
    [HttpPut("{id:guid}", Name = "UpdateVehicle")]
    [Authorize(Policy = "vehicles.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Update(Guid id, VehicleRequest request,
        [FromHeader(Name = "If-Match")] string version, CancellationToken ct) =>
        Mutation(await fleet.UpdateVehicle(Caller, id, new(request.Identifier, request.System), Version(version), ct), false, "");
    [HttpDelete("{id:guid}", Name = "DeleteVehicle")]
    [Authorize(Policy = "vehicles.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Deactivate(Guid id, [FromHeader(Name = "If-Match")] string version, CancellationToken ct) =>
        Mutation(await fleet.Deactivate(Caller, id, false, Version(version), ct), false, "");
    private async Task<IReadOnlyList<VehicleResponse>> Enrich(IReadOnlyList<FaultCode.Domain.Vehicle> vehicles, CancellationToken ct)
    {
        var state = await store.ReadAsync(Caller.TenantId, ct);
        return vehicles.Select(v => Map(v) with
        {
            Drivers = state.Associations.Where(a => a.VehicleId == v.Id && a.Until is null)
                .Select(a => state.Drivers.SingleOrDefault(d => d.Id == a.DriverId && d.TenantId == Caller.TenantId))
                .Where(d => d is not null && FleetService.CanRead(Caller, d)).Select(d => Map(d!)).ToArray()
        }).ToArray();
    }
}
