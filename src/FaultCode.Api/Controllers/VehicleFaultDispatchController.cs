using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FaultCode.Application.Incidents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaultCode.Api.Controllers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DispatchVehicleFaultRequest(Guid VehicleId, [Required, StringLength(60)] string Code);

[Route("api/v1/vehicle-fault-dispatches")]
[Authorize(Policy = "faults.write"), Authorize(Policy = "vehicles.access.read"), Authorize(Policy = "drivers.access.read")]
public sealed class VehicleFaultDispatchController(VehicleFaultDispatchService service) : FleetControllerBase
{
    [HttpPost(Name = "DispatchVehicleFault")]
    [ProducesResponseType<FaultDispatchReceipt>(202)]
    public async Task<ActionResult<FaultDispatchReceipt>> Dispatch(DispatchVehicleFaultRequest request,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        var receipt = await service.Dispatch(Caller, request.VehicleId, request.Code, Key(key), ct);
        return Accepted($"/api/v1/incidents/{receipt.IncidentId:D}", receipt);
    }
}
