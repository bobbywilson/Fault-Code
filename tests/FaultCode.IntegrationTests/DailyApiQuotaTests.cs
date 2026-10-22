using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using FaultCode.Application;
using FaultCode.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using FaultCode.Api.Identity;
using static FaultCode.IntegrationTests.EntraAuthenticationTests;

namespace FaultCode.IntegrationTests;

public sealed class DailyApiQuotaTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private static string AccessToken(SecurityKey key, string actor = Actor, string client = Client, bool machine = false)
    {
        var claims = new List<Claim> { new("tid", Tenant), new("ver", "2.0"), new("oid", actor), new("azp", client), new("jti", Guid.NewGuid().ToString("D")) };
        if (machine) { claims.Add(new("idtyp", "app")); claims.Add(new("roles", "Demo.Read.All")); }
        else claims.Add(new("scp", "demo.read"));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(Issuer, Audience, claims,
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10), new SigningCredentials(key, SecurityAlgorithms.RsaSha256)));
    }
    private static WebApplicationFactory<Program> Factory(Discovery keys, Clock clock, IApiRequestQuotaStore? store = null) =>
        CreateFactory(keys).WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock));
            s.Replace(ServiceDescriptor.Singleton<IApiRequestQuotaStore>(store ?? new DevelopmentApiRequestQuotaStore()));
        }));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exactly_200_authorized_requests_per_UTC_day_and_refresh_does_not_reset_quota(bool machine)
    {
        using var keys = new Discovery(); var clock = new Clock();
        await using var factory = Factory(keys, clock);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AccessToken(keys.Key, machine: machine));
        for (var i = 1; i <= 200; i++)
        {
            using var response = await client.GetAsync("/api/v1/quota-fixture");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("200", response.Headers.GetValues("X-RateLimit-Limit").Single());
            Assert.Equal((200 - i).ToString(), response.Headers.GetValues("X-RateLimit-Remaining").Single());
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AccessToken(keys.Key, machine: machine));
        using var blocked = await client.GetAsync("/api/v1/quota-fixture");
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal(TimeSpan.FromHours(12), blocked.Headers.RetryAfter!.Delta);
        Assert.Equal("application/problem+json", blocked.Content.Headers.ContentType!.MediaType);
        Assert.DoesNotContain(client.DefaultRequestHeaders.Authorization.Parameter!, await blocked.Content.ReadAsStringAsync());
        // Another subject has its own allowance, and public/unauthenticated requests stay public/401.
        client.DefaultRequestHeaders.Authorization = new("Bearer", AccessToken(keys.Key,
            actor: machine ? Actor : "55555555-5555-4555-8555-555555555555",
            client: machine ? "66666666-6666-4666-8666-666666666666" : Client, machine: machine));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/quota-fixture")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/quota-fixture")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        clock.Now = clock.Now.AddHours(12);
        client.DefaultRequestHeaders.Authorization = new("Bearer", AccessToken(keys.Key, machine: machine));
        using var tomorrow = await client.GetAsync("/api/v1/quota-fixture");
        Assert.Equal(HttpStatusCode.OK, tomorrow.StatusCode);
        Assert.Equal("199", tomorrow.Headers.GetValues("X-RateLimit-Remaining").Single());
    }

    [Fact]
    public async Task Concurrent_requests_never_admit_more_than_200_and_store_outage_fails_closed()
    {
        using var keys = new Discovery(); var clock = new Clock();
        await using var factory = Factory(keys, clock);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AccessToken(keys.Key));
        var responses = await Task.WhenAll(Enumerable.Range(0, 225).Select(_ => client.GetAsync("/api/v1/quota-fixture")));
        Assert.Equal(200, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(25, responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests));
        foreach (var response in responses) response.Dispose();
        await using var failed = Factory(keys, clock, new UnavailableApiRequestQuotaStore());
        using var unavailable = failed.CreateClient();
        unavailable.DefaultRequestHeaders.Authorization = new("Bearer", AccessToken(keys.Key));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await unavailable.GetAsync("/api/v1/quota-fixture")).StatusCode);
    }

    [Fact]
    public async Task John_browser_calls_and_his_application_token_share_one_daily_allowance()
    {
        var store = new DevelopmentApiRequestQuotaStore(); var clock = new Clock(); var executed = 0;
        var middleware = new DailyApiQuotaMiddleware(_ => { executed++; return Task.CompletedTask; });
        var options = Options.Create(new DemoApiTokenOptions { Enabled = true, ClientId = Client });
        DefaultHttpContext Context(bool browser)
        {
            var context = new DefaultHttpContext(); context.Request.Path = "/api/v1/vehicles";
            context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "quota"));
            var claims = new List<Claim> { new("tid", Tenant), new("oid", Actor), new("azp", Client) };
            if (!browser) claims.Add(new("idtyp", "app"));
            context.User = new(new ClaimsIdentity(claims, browser ? DemoSession.Scheme : "Bearer"));
            return context;
        }
        for (var i = 0; i < 200; i++) await middleware.InvokeAsync(Context(browser: true), store, clock, options);
        var tokenCall = Context(browser: false);
        await middleware.InvokeAsync(tokenCall, store, clock, options);
        Assert.Equal(200, executed); Assert.Equal(429, tokenCall.Response.StatusCode);
    }
}

[ApiController, Route("api/v1/quota-fixture"), Authorize(Policy = "IdentityFixture"), ApiExplorerSettings(IgnoreApi = true)]
public sealed class DailyQuotaFixtureController : ControllerBase
{
    [HttpGet] public IActionResult Get() => Ok(new { reached = true });
}
