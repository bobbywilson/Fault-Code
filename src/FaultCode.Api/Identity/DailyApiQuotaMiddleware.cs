using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FaultCode.Api.Configuration;
using FaultCode.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FaultCode.Api.Identity;

public sealed class DailyApiQuotaMiddleware(RequestDelegate next)
{
    public const int DailyLimit = 200;
    public async Task InvokeAsync(HttpContext context, IApiRequestQuotaStore store, TimeProvider clock, IOptions<DemoApiTokenOptions> demo)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1") || context.Request.Method == "OPTIONS"
            || context.GetEndpoint() is null || context.GetEndpoint()!.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || context.User.Identity?.IsAuthenticated != true)
        { await next(context); return; }

        var tenant = context.User.FindFirst("tid")?.Value ?? "";
        var machine = context.User.FindFirst("idtyp")?.Value == "app";
        var subject = context.User.FindFirst(machine ? "azp" : "oid")?.Value ?? "";
        // John's cookie login shares the dedicated application's API quota.
        if (context.User.Identity.AuthenticationType == DemoSession.Scheme && demo.Value.Enabled)
        { machine = true; subject = demo.Value.ClientId; }
        if (!EntraOptions.IsIdentifier(tenant) || !EntraOptions.IsIdentifier(subject))
        { context.Response.StatusCode = 403; return; }

        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes((machine ? "app:" : "user:") + subject)));
        var now = clock.GetUtcNow();
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        var reset = new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
        int? used;
        try { used = await store.Consume(tenant, identity, day, DailyLimit, context.RequestAborted); }
        catch (ApiQuotaUnavailableException) { context.Response.StatusCode = 503; return; }
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-RateLimit-Limit"] = DailyLimit.ToString(CultureInfo.InvariantCulture);
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, DailyLimit - (used ?? DailyLimit)).ToString(CultureInfo.InvariantCulture);
        context.Response.Headers["X-RateLimit-Reset"] = reset.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        if (used is null)
        {
            context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling((reset - now).TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            context.Response.StatusCode = 429; return;
        }
        await next(context);
    }
}
