using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FaultCode.Api.Authorization;
using FaultCode.Api.Contracts;
using FaultCode.Application.Administration;
using FaultCode.Application.Fleet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaultCode.Api.Controllers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReplaceUserRolesRequest([Required, MinLength(1), MaxLength(4)] string[] Roles);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReplaceIntegrationRequest([Required] bool? Enabled);
public sealed record RoleCatalogResponse(string[] Roles);

[Route("api/v1/admin")]
public sealed class AdministrationController(IAdministrationStore store) : FleetControllerBase
{
    internal static readonly string[] Roles = ["Reader", "Operator", "Engineer", "Administrator", "Driver"];
    private AdministrationAudit Audit => new(Caller.TenantId, Caller.ActorId, Caller.ClientId, HttpContext.TraceIdentifier);
    private static string Replay(string key) => key is { Length: >= 1 and <= 100 } && key.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? key : throw new FleetException(400);

    [HttpGet("roles", Name = "ListAdministrationRoles")]
    [Authorize(Policy = "users.manage")]
    [ProducesResponseType<RoleCatalogResponse>(200)]
    public RoleCatalogResponse Catalog() => new(Roles.ToArray());

    [HttpGet("users", Name = "ListManagedUsers")]
    [Authorize(Policy = "users.manage")]
    [ProducesResponseType<AdministrationPage<ManagedUser>>(200)]
    public Task<AdministrationPage<ManagedUser>> Users([FromQuery] FleetPageQuery query, CancellationToken ct) => store.Users(Audit, query.Page, query.PageSize, ct);

    [HttpGet("users/{id:guid}", Name = "GetManagedUser")]
    [Authorize(Policy = "users.manage")]
    [ProducesResponseType<ManagedUser>(200)]
    public async Task<ManagedUser> UserRecord(Guid id, CancellationToken ct)
    { var user = await store.User(Audit, id, ct); ETag(user.Version); return user; }

    [HttpPut("users/{id:guid}/roles", Name = "ReplaceManagedUserRoles")]
    [Authorize(Policy = "users.manage")]
    [ProducesResponseType<ManagedUser>(200)]
    public async Task<ManagedUser> ReplaceRoles(Guid id, ReplaceUserRolesRequest request,
        [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        if (request.Roles.Any(r => !Roles.Contains(r, StringComparer.Ordinal)) || request.Roles.Distinct(StringComparer.Ordinal).Count() != request.Roles.Length) throw new FleetException(400);
        var result = await store.ReplaceUser(Audit, id, request.Roles.Order(StringComparer.Ordinal).ToArray(), Version(version), Replay(key), ct);
        ETag(result.Version); return result;
    }

    [HttpGet("integrations", Name = "ListManagedIntegrations")]
    [Authorize(Policy = "integrations.manage")]
    [ProducesResponseType<AdministrationPage<ManagedIntegration>>(200)]
    public Task<AdministrationPage<ManagedIntegration>> Integrations([FromQuery] FleetPageQuery query, CancellationToken ct) => store.Integrations(Audit, query.Page, query.PageSize, ct);

    [HttpGet("integrations/{id:guid}", Name = "GetManagedIntegration")]
    [Authorize(Policy = "integrations.manage")]
    [ProducesResponseType<ManagedIntegration>(200)]
    public async Task<ManagedIntegration> Integration(Guid id, CancellationToken ct)
    { var integration = await store.Integration(Audit, id, ct); ETag(integration.Version); return integration; }

    [HttpPut("integrations/{id:guid}", Name = "ReplaceManagedIntegration")]
    [Authorize(Policy = "integrations.manage")]
    [ProducesResponseType<ManagedIntegration>(200)]
    public async Task<ManagedIntegration> ReplaceIntegration(Guid id, ReplaceIntegrationRequest request,
        [FromHeader(Name = "If-Match")] string version, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        var result = await store.ReplaceIntegration(Audit, id, request.Enabled!.Value, Version(version), Replay(key), ct);
        ETag(result.Version); return result;
    }
}
