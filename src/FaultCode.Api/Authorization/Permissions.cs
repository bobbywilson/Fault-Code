using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace FaultCode.Api.Authorization;

public enum CallerKind { Either, Delegated, Application }

public sealed record Permission(string Name, string Scope, string? ApplicationRole, IReadOnlyList<string> HumanRoles);

public static class Permissions
{
    private static Permission Read(string name, string appRole) => new(name, name, appRole,
        Array.AsReadOnly(new[] { "Reader", "Operator", "Engineer", "Administrator" }));
    private static Permission Write(string name, string appRole, string role) => new(name, name, appRole,
        Array.AsReadOnly(new[] { role }));

    public static IReadOnlyList<Permission> All { get; } = Array.AsReadOnly(new[]
    {
        Read("authorization.read", "Authorization.Read"),
        new Permission("drivers.read.own", "drivers.read.own", null, Array.AsReadOnly(new[] { "Reader" })),
        new Permission("vehicles.read.own", "vehicles.read.own", null, Array.AsReadOnly(new[] { "Reader" })),
        Read("drivers.read", "Drivers.Read.All"), Read("vehicles.read", "Vehicles.Read.All"),
        Write("drivers.write", "Drivers.Write.All", "Operator"), Write("vehicles.write", "Vehicles.Write.All", "Operator"),
        Read("faults.read", "Faults.Read.All"), Write("faults.write", "Faults.Write.All", "Operator"),
        Read("knowledge.read", "Knowledge.Read.All"), Write("knowledge.publish", "Knowledge.Publish", "Engineer"),
        Read("escalations.read", "Escalations.Read.All"), Write("escalations.write", "Escalations.Write", "Engineer"),
        Write("integrations.manage", "Integrations.Manage", "Administrator"),
        Write("users.manage", "Users.Manage", "Administrator")
    });

    public static Permission? Find(string name) => All.SingleOrDefault(p => p.Name == name);

    public static bool Allows(ClaimsPrincipal user, Permission permission, CallerKind kind = CallerKind.Either)
    {
        if (user.Identity?.IsAuthenticated != true) return false;
        var machine = user.FindFirst("idtyp")?.Value == "app";
        var scopes = user.FindAll("scp").SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToArray();
        var roles = user.FindAll("roles").Select(c => c.Value).ToArray();
        if (machine)
            return kind != CallerKind.Delegated && scopes.Length == 0 && permission.ApplicationRole is not null
                && roles.Contains(permission.ApplicationRole, StringComparer.Ordinal);
        return kind != CallerKind.Application && scopes.Contains(permission.Scope, StringComparer.Ordinal)
            && roles.Any(role => permission.HumanRoles.Contains(role, StringComparer.Ordinal));
    }

    public static bool TryPolicy(string policy, out Permission? permission, out CallerKind kind)
    {
        kind = policy.EndsWith(".Delegated", StringComparison.Ordinal) ? CallerKind.Delegated
            : policy.EndsWith(".Application", StringComparison.Ordinal) ? CallerKind.Application : CallerKind.Either;
        var name = kind == CallerKind.Either ? policy : policy[..policy.LastIndexOf('.')];
        permission = Find(name);
        return permission is not null;
    }

    public static IServiceCollection AddFaultCodeAuthorization(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
        services.AddSingleton<IAuthorizationHandler, DriverResourceHandler>();
        services.AddSingleton<IAuthorizationHandler, FleetReadHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("mcp.access", policy => policy.RequireAuthenticatedUser());
            options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAssertion(_ => false).Build();
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAssertion(context =>
                context.Resource is HttpContext http && http.GetEndpoint() is null).Build();
            foreach (var permission in All)
                foreach (var kind in Enum.GetValues<CallerKind>())
                {
                    var name = permission.Name + (kind == CallerKind.Either ? "" : "." + kind);
                    options.AddPolicy(name, policy => policy.RequireAuthenticatedUser()
                        .AddRequirements(new PermissionRequirement(permission, kind)));
                }
            foreach (var area in new[] { "drivers", "vehicles" })
                options.AddPolicy(area + ".access.read", policy => policy.RequireAuthenticatedUser()
                    .AddRequirements(new FleetReadRequirement(area)));
            options.AddPolicy("drivers.resource.read", policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new DriverResourceRequirement("drivers")));
            options.AddPolicy("vehicles.resource.read", policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new DriverResourceRequirement("vehicles")));
        });
        return services;
    }
}

public sealed record PermissionRequirement(Permission Permission, CallerKind Kind) : IAuthorizationRequirement;

public sealed class PermissionHandler(IHttpContextAccessor accessor, ILogger<PermissionHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var allowed = Permissions.Allows(context.User, requirement.Permission, requirement.Kind);
        if (allowed) context.Succeed(requirement);
        // Only trusted, validated GUID identifiers enter structured decision logs.
        logger.LogInformation("Authorization decision permission={Permission} kind={Kind} allowed={Allowed} actor={Actor} client={Client} correlationId={CorrelationId}",
            requirement.Permission.Name, requirement.Kind, allowed, Identifier(context.User, "oid"),
            Identifier(context.User, "azp"), accessor.HttpContext?.TraceIdentifier);
        return Task.CompletedTask;
    }

    internal static string? Identifier(ClaimsPrincipal user, string claim) =>
        Configuration.EntraOptions.IsIdentifier(user.FindFirst(claim)?.Value ?? "") ? user.FindFirst(claim)!.Value : null;
}
