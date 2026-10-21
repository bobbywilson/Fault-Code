using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FaultCode.Application.Administration;
using FaultCode.Application.Fleet;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using static FaultCode.IntegrationTests.EntraAuthenticationTests;

namespace FaultCode.IntegrationTests;

public sealed class AdministrationTests
{
    [Theory]
    [InlineData("users", "users.manage", "Users.Manage.Application")]
    [InlineData("integrations", "integrations.manage", "Integrations.Manage.Application")]
    public async Task Production_management_enforces_exact_role_scope_and_returns_unavailable(string area, string scope, string appRole)
    {
        using var keys = new Discovery();
        await using var factory = CreateFactory(keys);
        using var client = factory.CreateClient();
        var path = "/api/v1/admin/" + area;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        foreach (var role in new[] { "Reader", "Operator", "Engineer", "Administrator" })
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", AuthorizationTests.Token(keys.Key, scope, [role]));
            Assert.Equal(role == "Administrator" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthorizationTests.Token(keys.Key, "authorization.read", ["Administrator"]));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthorizationTests.Token(keys.Key, null, [appRole], true));
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(Tenant, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task User_roles_are_local_explicit_validated_and_use_trusted_audit_preconditions_and_replay()
    {
        using var keys = new Discovery();
        var store = new FixtureStore();
        await using var factory = CreateFactory(keys).WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<IAdministrationStore>(store)));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthorizationTests.Token(keys.Key, "users.manage", ["Administrator"]));
        var path = "/api/v1/admin/users/" + store.Id + "/roles";
        foreach (var payload in new object[] { new { roles = new[] { "Agent" } }, new { roles = new[] { "Reader", "Reader" } }, new { roles = Array.Empty<string>() }, new { roles = new[] { "Reader" }, tenant = Tenant } })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await Send(payload, "1", "one")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(new { roles = new[] { "Reader" } }, "W/1", "one")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(new { roles = new[] { "Reader" } }, "1", "invalid key")).StatusCode);
        var first = await Send(new { roles = new[] { "Engineer", "Reader" } }, "1", "one");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode); Assert.Equal("\"2\"", first.Headers.ETag?.Tag);
        var replay = await Send(new { roles = new[] { "Reader", "Engineer" } }, "1", "one");
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal(1, store.Commits); Assert.Equal(new[] { "Engineer", "Reader" }, store.Roles);
        Assert.Equal(Tenant, store.Audit!.Tenant); Assert.Equal(Actor, store.Audit.Actor); Assert.Equal(Client, store.Audit.Client);
        Assert.NotEmpty(store.Audit.CorrelationId);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(new { roles = new[] { "Reader" } }, "1", "two")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(new { roles = new[] { "Reader" } }, "2", "one")).StatusCode);
        // Token permissions are unchanged by local roles.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/vehicles")).StatusCode);
        async Task<HttpResponseMessage> Send(object body, string version, string key)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
            request.Headers.TryAddWithoutValidation("If-Match", version.StartsWith("W/", StringComparison.Ordinal) ? version : "\"" + version + "\"");
            request.Headers.Add("Idempotency-Key", key); return await client.SendAsync(request);
        }
    }

    [Fact]
    public async Task Lists_apply_tenant_before_count_and_bounds_and_other_tenant_detail_is_hidden()
    {
        using var keys = new Discovery(); var store = new FixtureStore();
        await using var factory = CreateFactory(keys).WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<IAdministrationStore>(store)));
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", AuthorizationTests.Token(keys.Key, "users.manage", ["Administrator"]));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/admin/users?pageSize=101")).StatusCode);
        var page = await client.GetFromJsonAsync<AdministrationPage<ManagedUser>>("/api/v1/admin/users?page=2&pageSize=1");
        Assert.Empty(page!.Items); Assert.Equal(1, page.TotalCount);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/admin/users/66666666-6666-4666-8666-666666666666")).StatusCode);
        var catalog = await client.GetStringAsync("/api/v1/admin/roles"); Assert.Contains("Administrator", catalog); Assert.DoesNotContain("Agent", catalog);
    }

    // Test-only single-process adapter; never production durability or Entra grant evidence.
    private sealed class FixtureStore : IAdministrationStore
    {
        public Guid Id { get; } = Guid.Parse("55555555-5555-4555-8555-555555555555");
        public string[] Roles = ["Reader"]; public int Commits; public AdministrationAudit? Audit;
        private readonly Dictionary<string, (string Payload, ManagedUser User)> replay = [];
        public Task<AdministrationPage<ManagedUser>> Users(AdministrationAudit c, int page, int pageSize, CancellationToken ct) => Task.FromResult(new AdministrationPage<ManagedUser>(c.Tenant == Tenant && page == 1 ? [new(Id, "Synthetic user", Roles, 1 + Commits)] : [], page, pageSize, c.Tenant == Tenant ? 1 : 0));
        public Task<ManagedUser> User(AdministrationAudit c, Guid id, CancellationToken ct) => c.Tenant == Tenant && id == Id ? Task.FromResult(new ManagedUser(Id, "Synthetic user", Roles, 1 + Commits)) : throw new FleetException(404);
        public async Task<ManagedUser> ReplaceUser(AdministrationAudit c, Guid id, string[] roles, long version, string key, CancellationToken ct)
        {
            await User(c, id, ct); var payload = string.Join(',', roles); var scoped = c.Tenant + c.Actor + c.Client + id + key;
            if (replay.TryGetValue(scoped, out var old)) return old.Payload == payload ? old.User : throw new FleetException(409);
            if (version != 1 + Commits) throw new FleetException(409);
            Roles = roles; Audit = c; Commits++; var result = new ManagedUser(id, "Synthetic user", roles, 1 + Commits); replay[scoped] = (payload, result); return result;
        }
        public Task<AdministrationPage<ManagedIntegration>> Integrations(AdministrationAudit c, int p, int s, CancellationToken ct) => throw new FleetException(503);
        public Task<ManagedIntegration> Integration(AdministrationAudit c, Guid id, CancellationToken ct) => throw new FleetException(503);
        public Task<ManagedIntegration> ReplaceIntegration(AdministrationAudit c, Guid id, bool enabled, long v, string key, CancellationToken ct) => throw new FleetException(503);
    }
}
