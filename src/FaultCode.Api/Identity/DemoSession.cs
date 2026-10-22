using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using FaultCode.Api.Authorization;
using FaultCode.Api.Configuration;
using Microsoft.AspNetCore.DataProtection;

namespace FaultCode.Api.Identity;

public sealed class DemoAccountOptions
{
    public bool Enabled { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ProfilePhoto { get; set; } = "";
    public string UserId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string Fingerprint => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(UserId + "\n" + ClientId + "\n" + TenantId + "\n" + Username + "\n" + PasswordHash + "\n" + DisplayName + "\n" + ProfilePhoto)));
    public bool Valid => !Enabled || Username.Length is >= 3 and <= 64 && Username.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')
        && !string.IsNullOrWhiteSpace(DisplayName) && DisplayName.Length <= 100
        && (ProfilePhoto == "" || System.Text.RegularExpressions.Regex.IsMatch(ProfilePhoto, @"^/profiles/[a-z0-9-]+\.png$"))
        && PasswordHash.Length is >= 60 and <= 512 && EntraOptions.IsIdentifier(UserId)
        && EntraOptions.IsIdentifier(ClientId) && EntraOptions.IsIdentifier(TenantId);
}

// Browser sessions are deliberately server-held: logout removes the ticket, and
// restarting this single-instance temporary demo invalidates all demo sessions.
public sealed class DemoTicketStore(TimeProvider clock) : ITicketStore
{
    private readonly ConcurrentDictionary<string, AuthenticationTicket> tickets = new();
    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        foreach (var pair in tickets.Where(p => p.Value.Properties.ExpiresUtc <= clock.GetUtcNow())) tickets.TryRemove(pair.Key, out _);
        if (tickets.Count >= 128) throw new InvalidOperationException("Demo session capacity reached.");
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); tickets[key] = ticket; return Task.FromResult(key);
    }
    public Task RenewAsync(string key, AuthenticationTicket ticket) { tickets[key] = ticket; return Task.CompletedTask; }
    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        if (tickets.TryGetValue(key, out var t) && t.Properties.ExpiresUtc > clock.GetUtcNow()) return Task.FromResult<AuthenticationTicket?>(t);
        tickets.TryRemove(key, out _); return Task.FromResult<AuthenticationTicket?>(null);
    }
    public Task RemoveAsync(string key) { tickets.TryRemove(key, out _); return Task.CompletedTask; }
}

public sealed class DemoLoginLimiter : IDisposable
{
    // One shared account: a global bound also prevents distributed password guessing.
    private readonly FixedWindowRateLimiter limiter = new(new() { PermitLimit = 8, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true });
    public RateLimitLease Acquire() => limiter.AttemptAcquire();
    public void Dispose() => limiter.Dispose();
}

public static class DemoSession
{
    public const string Scheme = "FaultCodeDemo";
    public static readonly string[] Grants = ["faults.read", "faults.write", "knowledge.read", "vehicles.read", "drivers.read"];
    private const string BrowserRequest = "FaultCode.BrowserSession";
    public static IServiceCollection AddDemoSession(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        // Tickets already live only for this process. Keep their encryption keys
        // in memory too so read-only containers never need a writable home directory.
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddOptions<DemoAccountOptions>().Bind(config.GetSection("DemoAccount")).Validate(o => o.Valid && (!o.Enabled || o.TenantId == config["Entra:TenantId"]), "Invalid demo account configuration.").ValidateOnStart();
        services.AddSingleton<DemoTicketStore>(); services.AddSingleton<DemoLoginLimiter>();
        services.AddOptions<DemoApiTokenOptions>().Bind(config.GetSection("DemoApiToken"))
            .Validate(o => !o.Enabled || EntraOptions.IsIdentifier(o.ClientId)
                && o.ClientId != config["Entra:ClientId"] && !string.IsNullOrWhiteSpace(o.CertificatePath)
                && !string.IsNullOrWhiteSpace(o.PrivateKeyPath) && bool.TryParse(config["Entra:Enabled"], out var enabled) && enabled,
                "Invalid dedicated demo API identity configuration.").ValidateOnStart();
        services.AddSingleton<IDemoApiTokenProvider, DemoApiTokenProvider>();
        services.AddAuthorization(o => o.AddPolicy("demo.token", new AuthorizationPolicyBuilder(Scheme).RequireAuthenticatedUser().Build()));
        services.AddSingleton<DemoTokenLimiter>();
        services.AddAntiforgery(o =>
        {
            o.HeaderName = "X-FaultCode-CSRF"; o.Cookie.Name = "FaultCode.Csrf"; o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict; o.Cookie.SecurePolicy = env.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        services.AddAuthentication(o => { o.DefaultScheme = "FaultCodeRequest"; o.DefaultAuthenticateScheme = "FaultCodeRequest"; o.DefaultChallengeScheme = "FaultCodeRequest"; })
            .AddPolicyScheme("FaultCodeRequest", null, o => o.ForwardDefaultSelector = c => c.Items.ContainsKey(BrowserRequest) || c.Request.Path.StartsWithSegments("/session") ? Scheme : JwtBearerDefaults.AuthenticationScheme)
            .AddCookie(Scheme, o =>
            {
                o.Cookie.Name = "FaultCode.DemoSession"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict;
                o.Cookie.SecurePolicy = env.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                o.ExpireTimeSpan = TimeSpan.FromHours(4); o.SlidingExpiration = false;
                o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
                o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
                o.Events.OnValidatePrincipal = c =>
                {
                    var a = c.HttpContext.RequestServices.GetRequiredService<IOptions<DemoAccountOptions>>().Value;
                    if (!a.Enabled || c.Principal?.FindFirst("demo-credential")?.Value != a.Fingerprint) c.RejectPrincipal();
                    return Task.CompletedTask;
                };
            });
        services.AddOptions<CookieAuthenticationOptions>(Scheme).Configure<DemoTicketStore>((o, store) => o.SessionStore = store);
        return services;
    }
    public static IApplicationBuilder UseDemoBrowserBoundary(this IApplicationBuilder app) => app.Use(async (c, next) =>
    {
        if (c.Request.Path.StartsWithSegments("/workspace/api/v1", out var rest))
        {
            c.Items[BrowserRequest] = true; c.Request.Path = "/api/v1" + rest;
            c.Response.Headers.CacheControl = "no-store";
        }
        await next(c);
    });
    public static IApplicationBuilder UseDemoAntiforgery(this IApplicationBuilder app) => app.Use(async (c, next) =>
    {
        var tokenIdentity = c.RequestServices.GetRequiredService<IOptions<DemoApiTokenOptions>>().Value;
        if (EntraOptions.IsIdentifier(tokenIdentity.ClientId) && c.User.Identity?.IsAuthenticated == true
            && c.User.FindFirst("idtyp")?.Value == "app" && c.User.FindFirst("azp")?.Value == tokenIdentity.ClientId
            && !AllowedDemoOperation(c.Request.Method, c.Request.Path.Value ?? ""))
        {
            c.Response.StatusCode = 403; return;
        }
        if (c.Items.ContainsKey(BrowserRequest) && c.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
        {
            if (!await ValidCsrf(c)) { c.Response.StatusCode = 400; return; }
            var path = c.Request.Path.Value ?? "";
            var allowed = AllowedDemoOperation(c.Request.Method, path);
            if (!allowed) { c.Response.StatusCode = 403; return; }
        }
        await next(c);
    });
    private static bool AllowedDemoOperation(string method, string path) => method is "GET" or "HEAD" or "OPTIONS"
        || method == "POST" && path == "/api/v1/incidents"
        || method == "PUT" && System.Text.RegularExpressions.Regex.IsMatch(path, @"^/api/v1/(incidents|notifications)/[0-9a-fA-F-]{36}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private static async Task<bool> ValidCsrf(HttpContext c)
    {
        if (c.Request.Headers["Sec-Fetch-Site"] == "cross-site") return false;
        try { await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
    private static object State(HttpContext c, DemoAccountOptions account)
    {
        c.Response.Headers.CacheControl = "no-store";
        var csrf = account.Enabled
            ? c.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(c).RequestToken : null;
        return new
        {
            demoAvailable = account.Enabled,
            csrfToken = csrf,
            user = account.Enabled && c.User.Identity?.IsAuthenticated == true ? new
            {
                id = account.UserId,
                displayName = account.DisplayName,
                profilePhoto = account.ProfilePhoto,
                email = "",
                roles = new[] { "Reader", "Operator" },
                permissions = Grants,
                tokenExpiresAt = (string?)null
            } : null
        };
    }
    public static void MapDemoSession(this WebApplication app)
    {
        // Browser infrastructure, intentionally absent from the bearer REST/OpenAPI contract.
        app.MapGet("/session/mailpit-access", () => Results.NoContent()).RequireAuthorization("demo.token").ExcludeFromDescription();
        app.MapGet("/session", (HttpContext c, IOptions<DemoAccountOptions> a) => Results.Json(State(c, a.Value))).AllowAnonymous().ExcludeFromDescription();
        app.MapPost("/session/api-token", async (HttpContext c, IDemoApiTokenProvider tokens, DemoTokenLimiter limiter) =>
        {
            c.Response.Headers.CacheControl = "no-store";
            c.Response.Headers.Pragma = "no-cache";
            if (!await ValidCsrf(c)) return Results.BadRequest();
            using var permit = limiter.Acquire();
            if (!permit.IsAcquired) { c.Response.Headers.RetryAfter = "60"; return Results.StatusCode(429); }
            // Refresh is a fixed query flag, never an arbitrary scope, client or tenant.
            if (!bool.TryParse(c.Request.Query["refresh"], out var refresh)) return Results.BadRequest();
            try { return Results.Json(await tokens.AcquireAsync(refresh, c.RequestAborted)); }
            catch (OperationCanceledException) when (c.RequestAborted.IsCancellationRequested) { throw; }
            catch { return Results.Problem(statusCode: 503, title: "Demo API token is unavailable."); }
        }).RequireAuthorization("demo.token").ExcludeFromDescription();
        app.MapPost("/session/login", async (HttpContext c, IOptions<DemoAccountOptions> options, DemoLoginLimiter limiter) =>
        {
            c.Response.Headers.CacheControl = "no-store";
            if (!options.Value.Enabled) return Results.NotFound();
            using var permit = limiter.Acquire(); if (!permit.IsAcquired) { c.Response.Headers.RetryAfter = "60"; return Results.StatusCode(429); }
            if (!await ValidCsrf(c)) return Results.BadRequest();
            if (c.Request.ContentLength is > 4096 || !c.Request.HasJsonContentType()) return Results.BadRequest();
            var limit = c.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>(); if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = 4096;
            DemoLogin? input; try { input = await c.Request.ReadFromJsonAsync<DemoLogin>(c.RequestAborted); } catch (System.Text.Json.JsonException) { return Results.BadRequest(); }
            var a = options.Value;
            if (input is null || string.IsNullOrEmpty(input.Password) || input.Password.Length > 256 || string.IsNullOrEmpty(input.Username) || input.Username.Length > 64) return Results.Unauthorized();
            PasswordVerificationResult verified;
            try { verified = new PasswordHasher<string>().VerifyHashedPassword(a.Username, a.PasswordHash, input.Password); }
            catch (FormatException) { return Results.Unauthorized(); }
            if (verified == PasswordVerificationResult.Failed || !string.Equals(input.Username, a.Username, StringComparison.Ordinal)) return Results.Unauthorized();
            var claims = new[] { new Claim("tid", a.TenantId), new Claim("oid", a.UserId), new Claim("azp", a.ClientId),
                new Claim("name", a.DisplayName), new Claim("roles", "Reader"), new Claim("roles", "Operator"), new Claim("scp", string.Join(' ', Grants)), new Claim("demo-credential", a.Fingerprint) };
            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
            await c.SignOutAsync(Scheme); // Rotate any existing session on sign-in.
            await c.SignInAsync(Scheme, user, new AuthenticationProperties { IsPersistent = false }); c.User = user;
            return Results.NoContent();
        }).AllowAnonymous().ExcludeFromDescription();
        app.MapPost("/session/logout", async (HttpContext c) =>
        {
            if (!await ValidCsrf(c)) return Results.BadRequest();
            await c.SignOutAsync(Scheme); c.User = new ClaimsPrincipal();
            return Results.NoContent();
        }).AllowAnonymous().ExcludeFromDescription();
    }
    private sealed record DemoLogin(string Username, string Password);
}

public sealed class DemoTokenLimiter : IDisposable
{
    private readonly FixedWindowRateLimiter limiter = new(new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true });
    public RateLimitLease Acquire() => limiter.AttemptAcquire();
    public void Dispose() => limiter.Dispose();
}
