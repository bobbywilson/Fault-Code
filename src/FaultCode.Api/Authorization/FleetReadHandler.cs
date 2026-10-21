using Microsoft.AspNetCore.Authorization;

namespace FaultCode.Api.Authorization;

public sealed record FleetReadRequirement(string Area) : IAuthorizationRequirement;
public sealed class FleetReadHandler(IHttpContextAccessor accessor, ILogger<FleetReadHandler> logger)
    : AuthorizationHandler<FleetReadRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, FleetReadRequirement requirement)
    {
        var allowed = Permissions.Allows(context.User, Permissions.Find(requirement.Area + ".read")!)
            || Permissions.Allows(context.User, Permissions.Find(requirement.Area + ".read.own")!);
        if (allowed) context.Succeed(requirement);
        logger.LogInformation("Authorization decision permission={Permission} allowed={Allowed} actor={Actor} client={Client} correlationId={CorrelationId}",
            requirement.Area + ".access.read", allowed, PermissionHandler.Identifier(context.User, "oid"),
            PermissionHandler.Identifier(context.User, "azp"), accessor.HttpContext?.TraceIdentifier);
        return Task.CompletedTask;
    }
}
