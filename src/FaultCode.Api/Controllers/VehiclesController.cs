using FaultCode.Api.Contracts;
using FaultCode.Api.Authorization;
using FaultCode.Application.Fleet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaultCode.Api.Controllers;

[Route("api/v1/vehicles")]
public sealed class VehiclesController(FleetService fleet, IAuthorizationService authorization) : FleetControllerBase
{
    [HttpGet(Name = "ListVehicles")]
    [Authorize(Policy = "vehicles.access.read")]
    [ProducesResponseType<PageResponse<VehicleResponse>>(200)]
    public async Task<ActionResult<PageResponse<VehicleResponse>>> List([FromQuery] FleetQuery query, CancellationToken ct)
    {
        var page = await fleet.Vehicles(Caller, query.Page, query.PageSize, query.Active, ct);
        return Ok(new PageResponse<VehicleResponse>(page.Items.Select(Map).ToArray(), page.Page, page.PageSize, page.TotalCount));
    }
    [HttpGet("{id:guid}", Name = "GetVehicle")]
    [Authorize(Policy = "vehicles.access.read")]
    [ProducesResponseType<VehicleResponse>(200)]
    public async Task<ActionResult<VehicleResponse>> Get(Guid id, CancellationToken ct)
    {
        var (v, d) = await fleet.GetVehicle(Caller, id, ct);
        if (!(await authorization.AuthorizeAsync(User, new DriverResource(d.TenantId, d.OwnerObjectId), "vehicles.resource.read")).Succeeded) return Forbid();
        ETag(v.Version); return Ok(Map(v));
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
    [HttpDelete("{id:guid}", Name = "DeactivateVehicle")]
    [Authorize(Policy = "vehicles.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> Deactivate(Guid id, [FromHeader(Name = "If-Match")] string version, CancellationToken ct) =>
        Mutation(await fleet.Deactivate(Caller, id, false, Version(version), ct), false, "");
    [HttpGet("{id:guid}/associations", Name = "ListVehicleAssociations")]
    [Authorize(Policy = "vehicles.access.read")]
    [ProducesResponseType<PageResponse<AssociationResponse>>(200)]
    public async Task<ActionResult<PageResponse<AssociationResponse>>> History(Guid id, [FromQuery] FleetPageQuery query, CancellationToken ct)
    {
        var page = await fleet.History(Caller, id, query.Page, query.PageSize, ct);
        return Ok(new PageResponse<AssociationResponse>(page.Items.Select(a => new AssociationResponse(a.Id, a.VehicleId, a.DriverId, a.From, a.Until)).ToArray(), page.Page, page.PageSize, page.TotalCount));
    }
    [HttpPost("{id:guid}/associations", Name = "AssociateVehicle")]
    [Authorize(Policy = "vehicles.write")]
    [Authorize(Policy = "drivers.write")]
    [ProducesResponseType<MutationResponse>(201)]
    public async Task<ActionResult<MutationResponse>> Associate(Guid id, AssociationRequest request,
        [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Mutation(await fleet.Associate(Caller, id, request.DriverId, Version(version), Key(key), ct), true, "/api/v1/vehicles/");
    [HttpDelete("{id:guid}/associations/{associationId:guid}", Name = "EndVehicleAssociation")]
    [Authorize(Policy = "vehicles.write")]
    [Authorize(Policy = "drivers.write")]
    [ProducesResponseType<MutationResponse>(200)]
    public async Task<ActionResult<MutationResponse>> End(Guid id, Guid associationId, [FromHeader(Name = "If-Match")] string version, CancellationToken ct) =>
        Mutation(await fleet.EndAssociation(Caller, id, associationId, Version(version), ct), false, "");
}
