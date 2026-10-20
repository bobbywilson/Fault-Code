using FaultCode.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaultCode.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet(Name = "GetApiHealth")]
    [ProducesResponseType<HealthResponse>(200)]
    [ProducesResponseType<ProblemDetails>(500, "application/problem+json")]
    public ActionResult<HealthResponse> Get() => Ok(new HealthResponse("Healthy"));
}
