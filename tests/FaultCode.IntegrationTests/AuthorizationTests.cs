using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using FaultCode.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using static FaultCode.IntegrationTests.EntraAuthenticationTests;

namespace FaultCode.IntegrationTests;

public sealed class AuthorizationTests
{
    internal static string Token(SecurityKey key, string? scope, string[] roles, bool machine = false, string actor = Actor)
    {
        var claims = new List<Claim> { new("tid", Tenant), new("ver", "2.0"), new("oid", actor), new("azp", Client) };
        if (scope is not null) claims.Add(new("scp", scope));
        if (machine) claims.Add(new("idtyp", "app"));
        claims.AddRange(roles.Select(r => new Claim("roles", r)));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(Issuer, Audience, claims,
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10), new SigningCredentials(key, SecurityAlgorithms.RsaSha256)));
    }

    [Fact]
    public async Task Every_permission_checks_exact_scope_role_and_caller_kind_through_real_JWT_middleware()
    {
        using var keys = new Discovery();
        await using var factory = CreateFactory(keys);
        using var client = factory.CreateClient();
        foreach (var permission in Permissions.All)
        {
            var expectedRoles = permission.Name switch
            {
                "authorization.read" => new[] { "Reader", "Operator", "Engineer", "Administrator", "Driver" },
                "drivers.read.own" or "vehicles.read.own" => new[] { "Reader", "Driver" },
                "drivers.write" or "vehicles.write" or "faults.write" => ["Operator"],
                "knowledge.publish" or "escalations.write" => ["Engineer"],
                "integrations.manage" or "users.manage" => ["Administrator"],
                _ => ["Reader", "Operator", "Engineer", "Administrator"]
            };
            Assert.Equal(permission.Name, permission.Scope);
            Assert.Equal(expectedRoles, permission.HumanRoles);
            foreach (var role in new[] { "Reader", "Operator", "Engineer", "Administrator", "Driver", "Agent", "reader" })
            {
                await Check(permission.Name, Token(keys.Key, permission.Scope, [role]), expectedRoles.Contains(role));
                await Check(permission.Name, Token(keys.Key, permission.Scope + ".extra", [role]), false);
            }
            await Check(permission.Name, Token(keys.Key, permission.Scope.ToUpperInvariant(), permission.HumanRoles.ToArray()), false);
            await Check(permission.Name, Token(keys.Key, "unrelated " + permission.Scope + " another", permission.HumanRoles.ToArray()), true);
            await Check(permission.Name + ".Application", Token(keys.Key, permission.Scope, permission.HumanRoles.ToArray()), false);
            await Check(permission.Name + ".Delegated", Token(keys.Key, permission.Scope, permission.HumanRoles.ToArray()), true);
            // An app role inside a delegated token never substitutes for the human role + scope.
            await Check(permission.Name, Token(keys.Key, "unrelated", [permission.ApplicationRole ?? "unrelated"]), false);
            if (permission.ApplicationRole is not null)
            {
                await Check(permission.Name, Token(keys.Key, null, [permission.ApplicationRole], true), true);
                await Check(permission.Name + ".Application", Token(keys.Key, null, [permission.ApplicationRole], true), true);
                await Check(permission.Name + ".Delegated", Token(keys.Key, null, [permission.ApplicationRole], true), false);
                await Check(permission.Name, Token(keys.Key, null, [permission.ApplicationRole.ToLowerInvariant()], true), false);
            }
            await Check(permission.Name, Token(keys.Key, null, ["Administrator"], true), false);
        }

        async Task Check(string policy, string token, bool allowed)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.GetAsync("/permission-fixture/policy/" + policy);
            Assert.True(response.StatusCode == (allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden),
                $"{policy}: expected allowed={allowed}, got {response.StatusCode}");
            if (!allowed) await Problem(response, 403, token);
        }
    }

    [Theory]
    [InlineData("drivers")]
    [InlineData("vehicles")]
    public async Task Ownership_requires_trusted_same_tenant_record_and_exact_own_or_all_permission(string area)
    {
        using var keys = new Discovery();
        await using var factory = CreateFactory(keys);
        using var client = factory.CreateClient();
        await Check(Token(keys.Key, area + ".read.own", ["Reader"]), "own", true);
        await Check(Token(keys.Key, area + ".read.own", ["Driver"]), "own", true);
        await Check(Token(keys.Key, area + ".read.own", ["Driver"], actor: Client), "own", false);
        await Check(Token(keys.Key, area + ".read", ["Driver"]), "own", false);
        await Check(Token(keys.Key, area + ".read.own", ["Reader"], actor: Client), "own", false);
        await Check(Token(keys.Key, area + ".read.own", ["Administrator"]), "own", false);
        await Check(Token(keys.Key, area + ".read", ["Operator"]), "other", true);
        var appRole = Permissions.Find(area + ".read")!.ApplicationRole!;
        await Check(Token(keys.Key, null, [appRole], true), "other", true);
        await Check(Token(keys.Key, null, [area + ".read.own"], true), "own", false);
        foreach (var record in new[] { "tenant", "unowned", "invalid", "missing" })
        {
            await Check(Token(keys.Key, area + ".read", ["Administrator"]), record, false);
            await Check(Token(keys.Key, null, [appRole], true), record, false);
        }
        // Request fields must never override the fixture's server-loaded owner.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(keys.Key, area + ".read.own", ["Reader"]));
        await Problem(await client.GetAsync($"/permission-fixture/resource/{area}/other?ownerObjectId={Actor}&tenantId={Tenant}"), 403);

        async Task Check(string token, string record, bool allowed)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.GetAsync($"/permission-fixture/resource/{area}/{record}");
            Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Unannotated_and_bare_authenticated_routes_deny_valid_identity_and_unknown_routes_stay_404()
    {
        using var keys = new Discovery();
        await using var factory = CreateFactory(keys);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(keys.Key, "authorization.read", ["Administrator"]));
        await Problem(await client.GetAsync("/permission-unannotated"), 403);
        await Problem(await client.GetAsync("/permission-bare"), 403);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/not-a-route")).StatusCode);
    }

    [Fact]
    public async Task Every_production_route_is_explicitly_anonymous_or_has_registered_permission_and_OpenAPI_security()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var json = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        Assert.NotEmpty(endpoints);
        var protectedCount = 0;
        foreach (var endpoint in endpoints)
        {
            if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null) continue;
            if (endpoint is RouteEndpoint mcp && mcp.RoutePattern.RawText?.StartsWith("/api/v1/mcp", StringComparison.Ordinal) == true)
            {
                Assert.All(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), p => Assert.Equal("mcp.access", p.Policy));
                Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
                Assert.False(json.RootElement.GetProperty("paths").TryGetProperty("/api/v1/mcp", out _));
                continue;
            }
            if (endpoint is RouteEndpoint session && session.RoutePattern.RawText is "/session/api-token" or "/session/mailpit-access")
            {
                var auth = Assert.Single(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
                Assert.Equal("demo.token", auth.Policy);
                var policy = await factory.Services.GetRequiredService<IAuthorizationPolicyProvider>().GetPolicyAsync("demo.token");
                Assert.Equal(new[] { FaultCode.Api.Identity.DemoSession.Scheme }, policy!.AuthenticationSchemes);
                Assert.False(json.RootElement.GetProperty("paths").TryGetProperty("/session/api-token", out _));
                continue;
            }
            if (endpoint is RouteEndpoint review && review.RoutePattern.RawText?.StartsWith("/workspace/tickets", StringComparison.Ordinal) == true)
            {
                var policy = Assert.Single(endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>());
                Assert.Contains(FaultCode.Api.Identity.DemoSession.Scheme, policy.AuthenticationSchemes);
                Assert.Contains("Bearer", policy.AuthenticationSchemes);
                var permission = Assert.Single(policy.Requirements.OfType<PermissionRequirement>());
                Assert.Equal("faults.read", permission.Permission.Name);
                Assert.Equal(CallerKind.Delegated, permission.Kind);
                Assert.False(json.RootElement.GetProperty("paths").TryGetProperty(review.RoutePattern.RawText!, out _));
                continue;
            }
            var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            Assert.NotEmpty(policies);
            foreach (var policy in policies)
            {
                var readArea = policy.Policy is "drivers.access.read" ? "drivers" : policy.Policy is "vehicles.access.read" ? "vehicles" : null;
                var known = Permissions.TryPolicy(policy.Policy ?? "", out var permission, out _);
                Assert.True(known || readArea is not null);
                Assert.NotNull(await factory.Services.GetRequiredService<IAuthorizationPolicyProvider>().GetPolicyAsync(policy.Policy!));
                var route = Assert.IsType<RouteEndpoint>(endpoint).RoutePattern.RawText!;
                route = System.Text.RegularExpressions.Regex.Replace(route, @":(?:guid|long)(?=})", "");
                var method = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single().ToLowerInvariant();
                var operation = json.RootElement.GetProperty("paths").GetProperty("/" + route.TrimStart('/')).GetProperty(method);
                Assert.True(operation.GetProperty("security")[0].TryGetProperty("EntraBearer", out _));
                Assert.Contains(policy.Policy, operation.GetProperty("x-faultcode-required-policies").EnumerateArray().Select(x => x.GetString()));
                var requirements = operation.GetProperty("x-faultcode-policy-requirements").EnumerateArray().ToArray();
                Assert.Equal(policies.Count, requirements.Length);
                var requirement = Assert.Single(requirements, x => x.GetProperty("policy").GetString() == policy.Policy);
                var alternatives = requirement.GetProperty("anyOf").EnumerateArray().ToArray();
                Assert.Equal(readArea is null ? 1 : 2, alternatives.Length);
                permission ??= Permissions.Find(readArea + ".read");
                Assert.Equal(permission!.Scope, alternatives[0].GetProperty("scope").GetString());
                Assert.Equal(permission.HumanRoles, alternatives[0].GetProperty("humanRoles").EnumerateArray().Select(x => x.GetString()));
                Assert.Equal(permission.ApplicationRole, alternatives[0].GetProperty("applicationRole").GetString());
                if (readArea is not null)
                {
                    Assert.Equal(readArea + ".read.own", alternatives[1].GetProperty("scope").GetString());
                    Assert.Equal(new[] { "Reader", "Driver" }, alternatives[1].GetProperty("humanRoles").EnumerateArray().Select(x => x.GetString()));
                    Assert.Equal(JsonValueKind.Null, alternatives[1].GetProperty("applicationRole").ValueKind);
                }
                if (policies.Count == 1)
                {
                    Assert.Equal(policy.Policy, operation.GetProperty("x-faultcode-permission").GetString());
                    permission ??= Permissions.Find(readArea + ".read");
                    Assert.Equal(permission!.Scope, operation.GetProperty("x-delegated-scopes")[0].GetString());
                    Assert.Equal(permission.ApplicationRole, operation.GetProperty("x-application-roles")[0].GetString());
                    Assert.Equal(permission.HumanRoles, operation.GetProperty("x-human-roles").EnumerateArray().Select(x => x.GetString()));
                }
            }
            protectedCount++;
        }
        Assert.Equal(30, protectedCount);
    }

    [Fact]
    public async Task Decision_logs_preserve_safe_actor_client_and_correlation_without_tokens_or_owner_payload()
    {
        using var keys = new Discovery();
        var logs = new DecisionLogs();
        await using var factory = CreateFactory(keys).WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = factory.CreateClient();
        var token = Token(keys.Key, "drivers.read knowledge.publish", ["Reader"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.GetAsync("/api/v1/drivers");
        var correlation = response.Headers.GetValues("X-Correlation-ID").Single();
        using var denial = await client.GetAsync("/permission-fixture/policy/knowledge.publish");
        var decisionText = string.Join("\n", logs.Messages);
        Assert.Contains("permission=drivers.access.read", decisionText);
        Assert.Contains("allowed=True", decisionText);
        Assert.Contains("permission=knowledge.publish", decisionText);
        Assert.Contains("allowed=False", decisionText);
        Assert.Contains(Actor, decisionText);
        Assert.Contains(Client, decisionText);
        Assert.Contains(correlation, decisionText);
        Assert.DoesNotContain(token, decisionText);
        Assert.DoesNotContain(Tenant, decisionText);
        Assert.DoesNotContain("Bearer", decisionText);
    }

    [Fact]
    public async Task Unprovisioned_identity_denies_the_production_permission_endpoint()
    {
        using var keys = new Discovery();
        await using var factory = CreateFactory(keys, enabled: false);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(keys.Key, "authorization.read", ["Reader"]));
        await Problem(await client.GetAsync("/api/v1/drivers"), 401);
        Assert.Empty(keys.Requests);
    }

    private sealed class DecisionLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new DecisionLogger(categoryName, Messages);
        public void Dispose() { }
        private sealed class DecisionLogger(string category, ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (category.StartsWith("FaultCode.Api.Authorization", StringComparison.Ordinal)) messages.Enqueue(formatter(state, exception));
            }
        }
    }

    private static async Task Problem(HttpResponseMessage response, int status, string? secret = null)
    {
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(status, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), json.RootElement.GetProperty("correlationId").GetString());
        if (secret is not null) Assert.DoesNotContain(secret, body);
    }
}

[ApiController]
[Authorize(Policy = "IdentityFixture")]
[Route("permission-fixture")]
public sealed class PermissionFixtureController(IAuthorizationService authorization) : ControllerBase
{
    [HttpGet("policy/{policy}")]
    public async Task<IActionResult> Policy(string policy) =>
        (await authorization.AuthorizeAsync(User, null, policy)).Succeeded ? Ok() : Forbid();

    [HttpGet("resource/{area}/{record}")]
    public async Task<IActionResult> Resource(string area, string record)
    {
        object? resource = record switch
        {
            "own" => new DriverResource(Tenant, Actor),
            "other" => new DriverResource(Tenant, Client),
            "tenant" => new DriverResource(Client, Actor),
            "unowned" => new DriverResource(Tenant, ""),
            "invalid" => new DriverResource(Tenant, "invalid"),
            _ => null
        };
        return (await authorization.AuthorizeAsync(User, resource, area + ".resource.read")).Succeeded ? Ok() : Forbid();
    }
}

[ApiController]
public sealed class UnannotatedPermissionController : ControllerBase
{
    [HttpGet("/permission-unannotated")]
    public IActionResult Unannotated() => Ok();
    [HttpGet("/permission-bare")]
    [Authorize]
    public IActionResult Bare() => Ok();
}
