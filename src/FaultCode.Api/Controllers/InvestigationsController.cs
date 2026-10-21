using FaultCode.Api.Contracts;
using FaultCode.Application.Investigations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaultCode.Api.Controllers;

[Route("api/v1/incidents/{id:guid}/investigations")]
[Authorize(Policy = "faults.read"), Authorize(Policy = "vehicles.access.read"), Authorize(Policy = "knowledge.read")]
public sealed class InvestigationsController(InvestigationService service) : FleetControllerBase
{
    [HttpPost(Name = "InvestigateIncident"), Authorize(Policy = "faults.write")]
    [ProducesResponseType<InvestigationResponse>(200)]
    [ProducesResponseType<ProblemDetails>(422, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(429, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(502, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(504, "application/problem+json")]
    public async Task<ActionResult<InvestigationResponse>> Run(Guid id, InvestigationRequest request,
        [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Ok(InvestigationResponse.From(await service.Run(Caller, id, Version(version), Key(key), ct)));

    [HttpGet(Name = "ListIncidentInvestigations")]
    [ProducesResponseType<PageResponse<InvestigationResponse>>(200)]
    public async Task<ActionResult<PageResponse<InvestigationResponse>>> List(Guid id, [FromQuery] InvestigationQuery query, CancellationToken ct)
    {
        var page = await service.List(Caller, id, query.Page, query.PageSize, ct);
        return Ok(new PageResponse<InvestigationResponse>(page.Items.Select(InvestigationResponse.From).ToArray(), page.Page, page.PageSize, page.TotalCount));
    }
}
