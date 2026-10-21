using System.Text.Json.Serialization;
using FaultCode.Application.Agents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaultCode.Api.Controllers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentRunRequest;

[Route("api/v1/incidents/{id:guid}/agent-runs")]
public sealed class AgentRunsController(AgentRuntimeService runtime) : FleetControllerBase
{
    [HttpPost(Name = "RunIncidentAgent"), Authorize(Policy = "faults.read"), Authorize(Policy = "vehicles.access.read"), Authorize(Policy = "knowledge.read")]
    [ProducesResponseType<AgentRunResult>(200)]
    [ProducesResponseType<ProblemDetails>(422, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(429, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(502, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(504, "application/problem+json")]
    public async Task<ActionResult<AgentRunResult>> Run(Guid id, AgentRunRequest request, CancellationToken ct) =>
        Ok(await runtime.Run(Caller, id, ct));
}
