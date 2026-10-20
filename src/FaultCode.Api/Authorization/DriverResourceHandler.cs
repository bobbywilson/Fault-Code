using Microsoft.AspNetCore.Authorization;

namespace FaultCode.Api.Authorization;

// Construct ONLY from trusted server-loaded records; never bind this from a request.
public sealed record DriverResource(string TenantId, string OwnerObjectId);
public sealed record DriverResourceRequirement(string Area) : IAuthorizationRequirement;

public sealed class DriverResourceHandler(IHttpContextAccessor accessor, ILogger<DriverResourceHandler> logger)
    : AuthorizationHandler<DriverResourceRequirement, DriverResource>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        DriverResourceRequirement requirement, DriverResource resource)
    {
        var tenant = PermissionHandler.Identifier(context.User, "tid");
        var actor = PermissionHandler.Identifier(context.User, "oid");
        var validResource = Configuration.EntraOptions.IsIdentifier(resource.TenantId)
            && Configuration.EntraOptions.IsIdentifier(resource.OwnerObjectId) && resource.TenantId == tenant;
        var all = Permissions.Find(requirement.Area + ".read");
        var own = Permissions.Find(requirement.Area + ".read.own");
        var allowed = validResource && actor is not null && all is not null && own is not null
            && (Permissions.Allows(context.User, all)
                || resource.OwnerObjectId == actor && Permissions.Allows(context.User, own, CallerKind.Delegated));
        if (allowed) context.Succeed(requirement);
        logger.LogInformation("Resource authorization area={Area} allowed={Allowed} actor={Actor} client={Client} correlationId={CorrelationId}",
            requirement.Area, allowed, actor, PermissionHandler.Identifier(context.User, "azp"), accessor.HttpContext?.TraceIdentifier);
        return Task.CompletedTask;
    }
}
