using System.Globalization;
using FaultCode.Api.Authorization;
using FaultCode.Api.Contracts;
using FaultCode.Application.Fleet;
using Microsoft.AspNetCore.Mvc;

namespace FaultCode.Api.Controllers;

[ApiController]
[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(503, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(500, "application/problem+json")]
public abstract class FleetControllerBase : ControllerBase
{
    protected FleetCaller Caller => new(PermissionHandler.Identifier(User, "tid") ?? "", PermissionHandler.Identifier(User, "oid") ?? "",
        PermissionHandler.Identifier(User, "azp") ?? "", HttpContext.TraceIdentifier,
        Allows("drivers.read"), Allows("drivers.read.own"), Allows("vehicles.read"), Allows("vehicles.read.own"));
    private bool Allows(string name) => Permissions.Allows(User, Permissions.Find(name)!);
    protected long Version(string value)
    {
        if (value.Length < 3 || value[0] != '"' || value[^1] != '"'
            || !long.TryParse(value[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            || version < 1 || value != $"\"{version}\"") throw new FleetException(400);
        return version;
    }
    protected static string Key(string? value) => string.IsNullOrEmpty(value) ? throw new FleetException(400) : value;
    protected void ETag(long version) => Response.Headers.ETag = $"\"{version}\"";
    protected ActionResult<MutationResponse> Mutation(MutationResult result, bool created, string path)
    {
        ETag(result.Version);
        var body = new MutationResponse(result.Id, result.Version);
        return created ? Created(path + result.Id, body) : Ok(body);
    }
    protected static DriverResponse Map(FaultCode.Domain.Driver d) => new(d.Id, d.Identifier, d.DisplayName, d.Email, d.EmailNotifications, d.Active, d.Version);
    protected static VehicleResponse Map(FaultCode.Domain.Vehicle v) => new(v.Id, v.Identifier, v.System, v.Active, v.Version);
}
