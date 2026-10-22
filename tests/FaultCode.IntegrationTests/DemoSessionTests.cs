using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FaultCode.Api.Identity;
using static FaultCode.IntegrationTests.EntraAuthenticationTests;
namespace FaultCode.IntegrationTests;

public sealed class DemoSessionTests
{
    private const string Password = "Only-for-isolated-demo-session-tests!";
    private static WebApplicationFactory<Program> Factory(Discovery keys, TimeProvider? clock = null) => CreateFactory(keys).WithWebHostBuilder(b =>
    {
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DemoAccount:Enabled"] = "true",
            ["DemoAccount:DisplayName"] = "John Henry",
            ["DemoAccount:ProfilePhoto"] = "",
            ["DemoAccount:Username"] = "demo",
            ["DemoAccount:PasswordHash"] = new PasswordHasher<string>().HashPassword("demo", Password),
            ["DemoAccount:UserId"] = Actor,
            ["DemoAccount:ClientId"] = Client,
            ["DemoAccount:TenantId"] = Tenant
        }));
        if (clock is not null) b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton(clock)));
    });
    private static async Task<JsonObject> Session(HttpClient c) => (await c.GetFromJsonAsync<JsonObject>("/session"))!;
    private static async Task<HttpResponseMessage> Post(HttpClient c, string path, string? csrf, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body ?? new { }) };
        request.Headers.Add("Idempotency-Key", "demo-session-test");
        if (csrf is not null) request.Headers.Add("X-FaultCode-CSRF", csrf);
        return await c.SendAsync(request);
    }
    private static async Task<JsonObject> Login(HttpClient c)
    {
        var s = await Session(c); using var r = await Post(c, "/session/login", s["csrfToken"]!.GetValue<string>(), new { username = "demo", password = Password });
        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
        Assert.Contains(r.Headers.GetValues("Set-Cookie"), v => v.Contains("FaultCode.DemoSession=") && v.Contains("httponly", StringComparison.OrdinalIgnoreCase) && v.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));
        return await Session(c);
    }
    [Fact]
    public async Task Browser_credentials_are_server_verified_and_never_become_an_Entra_token_or_admin_grant()
    {
        using var keys = new Discovery(); await using var f = Factory(keys); using var c = f.CreateClient();
        var s = await Session(c); Assert.True(s["demoAvailable"]!.GetValue<bool>()); Assert.Null(s["user"]);
        using (var csrfMissing = await Post(c, "/session/login", null, new { username = "demo", password = Password })) Assert.Equal(HttpStatusCode.BadRequest, csrfMissing.StatusCode);
        using (var wrong = await Post(c, "/session/login", s["csrfToken"]!.GetValue<string>(), new { username = "demo", password = "wrong" })) Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        s = await Login(c);
        Assert.Equal("John Henry", s["user"]!["displayName"]!.GetValue<string>());
        Assert.Equal("", s["user"]!["profilePhoto"]!.GetValue<string>());
        Assert.DoesNotContain("tokenExpiresAt\":\"", s.ToJsonString()); Assert.DoesNotContain("Password", s.ToJsonString());
        var scopes = s["user"]!["permissions"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
        Assert.Contains("faults.write", scopes); Assert.DoesNotContain("users.manage", scopes); Assert.DoesNotContain("knowledge.publish", scopes);
        using (var external = await c.GetAsync("/api/v1/vehicles")) Assert.Equal(HttpStatusCode.Unauthorized, external.StatusCode);
        // Authorized session reaches the real registered service boundary, which is deliberately unavailable in this fixture.
        using (var browser = await c.GetAsync("/workspace/api/v1/vehicles")) Assert.Equal(HttpStatusCode.ServiceUnavailable, browser.StatusCode);
        using (var validCsrf = await Post(c, "/workspace/api/v1/incidents", s["csrfToken"]!.GetValue<string>(), new { vehicleId = Guid.NewGuid(), code = "SYN-EPS:UNKNOWN" })) Assert.Equal(HttpStatusCode.ServiceUnavailable, validCsrf.StatusCode);
        using (var absentCsrf = await Post(c, "/workspace/api/v1/incidents", null, new { vehicleId = Guid.NewGuid(), code = "SYN-EPS:UNKNOWN" })) Assert.Equal(HttpStatusCode.BadRequest, absentCsrf.StatusCode);
        using (var publication = await Post(c, "/workspace/api/v1/fault-codes", s["csrfToken"]!.GetValue<string>(), new { })) Assert.Equal(HttpStatusCode.Forbidden, publication.StatusCode);
        using (var logout = await Post(c, "/session/logout", s["csrfToken"]!.GetValue<string>())) Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using (var after = await c.GetAsync("/workspace/api/v1/vehicles")) Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Null((await Session(c))["user"]);
    }
    [Fact]
    public async Task Disabled_demo_cannot_sign_in_or_change_the_bearer_API()
    {
        using var keys = new Discovery(); await using var f = CreateFactory(keys); using var c = f.CreateClient();
        Assert.False((await Session(c))["demoAvailable"]!.GetValue<bool>());
        using var r = await Post(c, "/session/login", null, new { username = "demo", password = Password }); Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        using var api = await c.GetAsync("/api/v1/vehicles"); Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
    }
    [Fact]
    public async Task Repeated_bad_credentials_are_rate_limited()
    {
        using var keys = new Discovery(); await using var f = Factory(keys); using var c = f.CreateClient(); var csrf = (await Session(c))["csrfToken"]!.GetValue<string>();
        for (var i = 0; i < 8; i++) { using var r = await Post(c, "/session/login", csrf, new { username = "demo", password = "wrong" }); Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode); }
        using var limited = await Post(c, "/session/login", csrf, new { username = "demo", password = Password }); Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }
    [Fact]
    public async Task Expired_server_ticket_is_rejected()
    {
        var clock = new DemoClock(); using var keys = new Discovery(); await using var f = Factory(keys, clock); using var c = f.CreateClient(); await Login(c);
        clock.Now = clock.Now.AddHours(5);
        using var r = await c.GetAsync("/workspace/api/v1/vehicles"); Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }
    [Fact]
    public async Task Logout_revokes_a_replayed_cookie_and_tampering_is_rejected()
    {
        using var keys = new Discovery(); await using var f = Factory(keys); using var c = f.CreateClient(); var csrf = (await Session(c))["csrfToken"]!.GetValue<string>();
        using var login = await Post(c, "/session/login", csrf, new { username = "demo", password = Password });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var cookie = login.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("FaultCode.DemoSession=", StringComparison.Ordinal) && !x.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase)).Split(';')[0];
        using var replay = f.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false }); replay.DefaultRequestHeaders.Add("Cookie", cookie);
        using (var before = await replay.GetAsync("/workspace/api/v1/vehicles")) Assert.Equal(HttpStatusCode.ServiceUnavailable, before.StatusCode);
        csrf = (await Session(c))["csrfToken"]!.GetValue<string>(); using (var logout = await Post(c, "/session/logout", csrf)) Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using (var after = await replay.GetAsync("/workspace/api/v1/vehicles")) Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        replay.DefaultRequestHeaders.Remove("Cookie"); replay.DefaultRequestHeaders.Add("Cookie", cookie + "tampered");
        using var tampered = await replay.GetAsync("/workspace/api/v1/vehicles"); Assert.Equal(HttpStatusCode.Unauthorized, tampered.StatusCode);
    }
    [Fact]
    public async Task Demo_application_token_is_cookie_and_CSRF_gated_and_never_exposes_provider_errors()
    {
        using var keys = new Discovery(); var tokens = new TestDemoTokens();
        await using var f = Factory(keys).WithWebHostBuilder(b => b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<FaultCode.Api.Identity.IDemoApiTokenProvider>(tokens))));
        using var c = f.CreateClient();
        using (var anonymous = await Post(c, "/session/api-token?refresh=false", null)) Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(0, tokens.Calls);
        var csrf = (await Login(c))["csrfToken"]!.GetValue<string>();
        using (var badCsrf = await Post(c, "/session/api-token?refresh=false", null)) Assert.Equal(HttpStatusCode.BadRequest, badCsrf.StatusCode);
        using (var badFlag = await Post(c, "/session/api-token?refresh=invalid", csrf)) Assert.Equal(HttpStatusCode.BadRequest, badFlag.StatusCode);
        using (var response = await Post(c, "/session/api-token?refresh=false", csrf))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
            var body = await response.Content.ReadAsStringAsync(); Assert.Contains("test-demo-application-token", body); Assert.DoesNotContain("credential", body);
        }
        using (var refresh = await Post(c, "/session/api-token?refresh=true", csrf)) Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.True(tokens.Refresh); Assert.Equal(2, tokens.Calls);
        tokens.Fail = true;
        using (var failure = await Post(c, "/session/api-token?refresh=true", csrf)) { Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode); Assert.DoesNotContain("private-provider-detail", await failure.Content.ReadAsStringAsync()); }
        using (var logout = await Post(c, "/session/logout", csrf)) Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using (var after = await Post(c, "/session/api-token?refresh=false", csrf)) Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Equal(3, tokens.Calls);
    }
    [Fact]
    public async Task Demo_bearer_application_cannot_bypass_the_browser_mutation_boundary()
    {
        using var keys = new Discovery();
        await using var f = Factory(keys).WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["DemoApiToken:ClientId"] = Client })));
        using var client = f.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthorizationTests.Token(keys.Key, null, FaultCode.Api.Identity.DemoApiTokenProvider.RequiredRoles, true));
        using (var read = await client.GetAsync("/api/v1/vehicles")) Assert.Equal(HttpStatusCode.ServiceUnavailable, read.StatusCode);
        client.DefaultRequestHeaders.Add("Idempotency-Key", "demo-bearer-test");
        using (var create = await client.PostAsJsonAsync("/api/v1/incidents", new { vehicleId = Guid.NewGuid(), code = "test" })) Assert.Equal(HttpStatusCode.ServiceUnavailable, create.StatusCode);
        using (var delete = await client.DeleteAsync("/api/v1/incidents/" + Guid.NewGuid())) Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        using (var deleteNotification = await client.DeleteAsync("/api/v1/notifications/" + Guid.NewGuid())) Assert.Equal(HttpStatusCode.Forbidden, deleteNotification.StatusCode);
        using (var modifyFleet = await client.PutAsJsonAsync("/api/v1/vehicles/" + Guid.NewGuid(), new { })) Assert.Equal(HttpStatusCode.Forbidden, modifyFleet.StatusCode);
        using (var tokenRoute = await client.PostAsJsonAsync("/session/api-token?refresh=false", new { })) Assert.Equal(HttpStatusCode.Unauthorized, tokenRoute.StatusCode);
    }
    private sealed class TestDemoTokens : FaultCode.Api.Identity.IDemoApiTokenProvider
    {
        public int Calls; public bool Refresh; public bool Fail;
        public Task<FaultCode.Api.Identity.DemoApiTokenResult> AcquireAsync(bool refresh, CancellationToken cancellationToken) { Calls++; Refresh = refresh; if (Fail) throw new InvalidOperationException("private-provider-detail"); return Task.FromResult(new FaultCode.Api.Identity.DemoApiTokenResult("test-demo-application-token", DateTimeOffset.UtcNow.AddMinutes(30), Client)); }
    }
    private sealed class DemoClock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
}
