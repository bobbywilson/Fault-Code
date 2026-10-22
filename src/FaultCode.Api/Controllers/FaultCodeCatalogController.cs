using FaultCode.Api.Authorization;
using FaultCode.Application.Knowledge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FaultCode.Api.Controllers;
public sealed record DemoFaultCodeRow(string Code, string System, string Description, string[] Vehicles);
public sealed record DemoFaultCodeCatalog(string Scope, DemoFaultCodeRow[] Items);
[ApiController, Route("api/v1/fault-code-catalog")]
public sealed class FaultCodeCatalogController(IKnowledgeStore store) : ControllerBase
{
    [HttpGet(Name = "ListDemoFaultCodes"), Authorize(Policy = "knowledge.read")]
    [ProducesResponseType<DemoFaultCodeCatalog>(200)]
    public async Task<ActionResult<DemoFaultCodeCatalog>> List(CancellationToken ct)
    {
        var tenant = PermissionHandler.Identifier(User, "tid") ?? "";
        var state = await store.ReadAsync(tenant, ct);
        var rows = state.Faults.Where(f => f.TenantId == tenant && !f.Retired).OrderBy(f => f.Code, StringComparer.Ordinal)
            .Select(f => new DemoFaultCodeRow(f.Code, f.System, f.Draft.Description, ["Chevrolet Silverado", "GMC Sierra"])).ToArray();
        return Ok(new DemoFaultCodeCatalog("Demonstration steering scenarios for the test fleet; not verified manufacturer DTCs or vehicle compatibility claims.", rows));
    }
}
