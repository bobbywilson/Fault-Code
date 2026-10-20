using FaultCode.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaultCode.Api.Controllers;

public sealed record CallerPermissions(string CallerKind, string[] Permissions);

[ApiController]
[Route("api/v1/authorization")]
public sealed class AuthorizationController : ControllerBase
{
    [HttpGet("me", Name = "GetCallerPermissions")]
    [Authorize(Policy = "authorization.read")]
    [ProducesResponseType<CallerPermissions>(200)]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
    public CallerPermissions Get() => new(User.FindFirst("idtyp")?.Value == "app" ? "application" : "delegated",
        Authorization.Permissions.All.Where(p => Authorization.Permissions.Allows(User, p))
            .Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
}
