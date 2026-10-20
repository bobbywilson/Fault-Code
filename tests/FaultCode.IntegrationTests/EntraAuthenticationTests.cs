using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FaultCode.IntegrationTests;

public sealed class EntraAuthenticationTests
{
    internal const string Tenant = "11111111-1111-4111-8111-111111111111";
    internal const string Audience = "22222222-2222-4222-8222-222222222222";
    internal const string Actor = "33333333-3333-4333-8333-333333333333";
    internal const string Client = "44444444-4444-4444-8444-444444444444";
    internal const string Issuer = "https://login.microsoftonline.com/" + Tenant + "/v2.0";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_discovery_and_signed_user_or_machine_tokens_authenticate(bool machine)
    {
        using var keys = new Discovery();
        await using var factory = CreateFactory(keys);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(keys.Key, machine: machine));
        var response = await client.GetAsync("/identity-fixture");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(Actor, body);
        Assert.Contains(machine ? "Synthetic.Read.All" : "synthetic.read", body);
        Assert.Contains(keys.Requests, x => x.EndsWith("/.well-known/openid-configuration", StringComparison.Ordinal));
        Assert.Contains(keys.Requests, x => x.EndsWith("/keys", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("unsigned")]
    [InlineData("wrong-algorithm")]
    [InlineData("wrong-key")]
    [InlineData("wrong-audience")]
    [InlineData("audience-slash")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-tenant")]
    [InlineData("wrong-version")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("no-expiry")]
    [InlineData("no-actor")]
    [InlineData("no-client")]
    [InlineData("id-token")]
    [InlineData("roleless-app")]
    [InlineData("app-with-scope")]
    [InlineData("metadata-outage")]
    [InlineData("unknown-key")]
    public async Task Invalid_identity_fails_closed_without_disclosing_token_or_reason(string failure)
    {
        using var keys = new Discovery(failure == "metadata-outage", failure == "wrong-issuer");
        using var other = RSA.Create(2048);
        var signingKey = failure is "wrong-key" or "unknown-key"
            ? new RsaSecurityKey(other) { KeyId = failure == "wrong-key" ? keys.Key.KeyId : "unknown" } : keys.Key;
        await using var factory = CreateFactory(keys);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        var token = failure == "malformed" ? "secret-invalid-token" : Token(signingKey, failure);
        if (failure != "missing") client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/identity-fixture");
        await AssertDenied(response, token);
    }

    [Fact]
    public async Task Authenticated_identity_is_not_an_automatic_operation_permission()
    {
        using var keys = new Discovery();
        await using var factory = CreateFactory(keys);
        using var client = factory.CreateClient();
        await AssertDenied(await client.GetAsync("/identity-fixture/restricted"), "no-token");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(keys.Key, machine: true));
        var response = await client.GetAsync("/identity-fixture/restricted");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(403, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), json.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task Disabled_identity_never_uses_discovery_or_accepts_a_token()
    {
        using var keys = new Discovery();
        await using var factory = CreateFactory(keys, enabled: false);
        using var client = factory.CreateClient();
        var token = Token(keys.Key);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await AssertDenied(await client.GetAsync("/identity-fixture"), token);
        Assert.Empty(keys.Requests);
        foreach (var path in new[] { "/api/v1/health", "/api/v1/examples", "/openapi/v1.json", "/health/live", "/health/ready" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/not-an-endpoint")).StatusCode);
    }

    [Fact]
    public async Task Public_operations_stay_anonymous_with_enabled_identity()
    {
        using var keys = new Discovery();
        await using var factory = CreateFactory(keys);
        using var client = factory.CreateClient();
        foreach (var path in new[] { "/api/v1/health", "/api/v1/examples", "/openapi/v1.json", "/health/live", "/health/ready" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        Assert.Empty(keys.Requests);
    }

    [Fact]
    public async Task Test_identity_routes_do_not_ship()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/identity-fixture")).StatusCode);
    }

    internal static WebApplicationFactory<Program> CreateFactory(Discovery discovery, bool enabled = true) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Entra:Enabled"] = enabled.ToString(),
                ["Entra:TenantId"] = Tenant,
                ["Entra:ClientId"] = Audience
            }));
            builder.ConfigureServices(services =>
            {
                services.AddAuthorization(options => options.AddPolicy("IdentityFixture", policy => policy.RequireAuthenticatedUser()));
                services.AddControllers().AddApplicationPart(typeof(IdentityFixtureController).Assembly);
                services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                    options.Backchannel = new HttpClient(discovery, disposeHandler: false));
            });
        });

    internal static string Token(SecurityKey key, string failure = "", bool machine = false)
    {
        var claims = new List<Claim>
        {
            new("tid", failure == "wrong-tenant" ? Client : Tenant), new("ver", failure == "wrong-version" ? "1.0" : "2.0"),
            new("oid", Actor), new("azp", Client), new("sub", Actor)
        };
        if (failure == "no-actor") claims.RemoveAll(x => x.Type == "oid");
        if (failure == "no-client") claims.RemoveAll(x => x.Type == "azp");
        if (machine || failure is "roleless-app" or "app-with-scope")
        {
            claims.Add(new("idtyp", "app"));
            if (failure != "roleless-app") claims.Add(new("roles", "Synthetic.Read.All"));
            if (failure == "app-with-scope") claims.Add(new("scp", "synthetic.read"));
        }
        else if (failure != "id-token") claims.Add(new("scp", "synthetic.read"));
        var now = DateTime.UtcNow;
        var jwt = new JwtSecurityToken(failure == "wrong-issuer" ? "https://unrelated.invalid/v2.0" : Issuer,
            failure == "wrong-audience" ? Client : failure == "audience-slash" ? Audience + "/" : Audience,
            claims, now.AddMinutes(failure == "future" ? 10 : -20),
            failure == "no-expiry" ? null : now.AddMinutes(failure == "expired" ? -10 : 20),
            failure == "unsigned" ? null : new SigningCredentials(key,
                failure == "wrong-algorithm" ? SecurityAlgorithms.RsaSha384 : SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private static async Task AssertDenied(HttpResponseMessage response, string token)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().ToString());
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(401, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), json.RootElement.GetProperty("correlationId").GetString());
        Assert.DoesNotContain(token, body);
        Assert.DoesNotContain("IDX", body);
        Assert.DoesNotContain(Tenant, body);
        Assert.False(json.RootElement.TryGetProperty("detail", out _));
    }

    internal sealed class Discovery : HttpMessageHandler
    {
        private readonly RSA rsa = RSA.Create(2048);
        private readonly bool outage;
        private readonly bool wrongIssuer;
        public RsaSecurityKey Key { get; }
        public List<string> Requests { get; } = [];

        public Discovery(bool outage = false, bool wrongIssuer = false)
        {
            this.outage = outage;
            this.wrongIssuer = wrongIssuer;
            Key = new RsaSecurityKey(rsa) { KeyId = "synthetic-key" };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);
            if (outage) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(Key);
            var document = request.RequestUri.AbsolutePath.EndsWith("/keys", StringComparison.Ordinal)
                ? JsonSerializer.Serialize(new { keys = new[] { new { kty = "RSA", use = "sig", kid = Key.KeyId, n = jwk.N, e = jwk.E, alg = "RS256" } } })
                : JsonSerializer.Serialize(new { issuer = wrongIssuer ? "https://unrelated.invalid/v2.0" : Issuer, jwks_uri = Issuer + "/keys" });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(document) });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) rsa.Dispose();
            base.Dispose(disposing);
        }
    }
}

[ApiController]
[Route("identity-fixture")]
public sealed class IdentityFixtureController : ControllerBase
{
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "IdentityFixture")]
    [HttpGet]
    public object Get() => new { actor = User.FindFirst("oid")?.Value, scopes = User.FindFirst("scp")?.Value, roles = User.FindAll("roles").Select(x => x.Value) };

    [HttpGet("restricted")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Synthetic.Admin")]
    public object Restricted() => Get();
}
