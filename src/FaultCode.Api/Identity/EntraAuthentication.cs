using FaultCode.Api.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace FaultCode.Api.Identity;

public static class EntraAuthentication
{
    public static IServiceCollection AddEntraAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("Entra");
        services.AddOptions<EntraOptions>().Configure(options =>
            {
                var enabled = section["Enabled"];
                if (enabled is not null && !bool.TryParse(enabled, out _))
                    throw new OptionsValidationException(nameof(EntraOptions), typeof(EntraOptions),
                        ["Entra:Enabled must be true or false."]);
                options.Enabled = bool.TryParse(enabled, out var value) && value;
                options.TenantId = section["TenantId"] ?? "";
                options.ClientId = section["ClientId"] ?? "";
            })
            .Validate(o => !o.Enabled || EntraOptions.IsIdentifier(o.TenantId),
                "Entra:TenantId must be a canonical nonempty tenant GUID when enabled.")
            .Validate(o => !o.Enabled || EntraOptions.IsIdentifier(o.ClientId),
                "Entra:ClientId must be a canonical nonempty API client GUID when enabled.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<EntraOptions>>((options, configured) =>
        {
            var identity = configured.Value;
            options.MapInboundClaims = false;
            options.SaveToken = false;
            options.IncludeErrorDetails = false;
            options.RequireHttpsMetadata = true;
            if (identity.Enabled && EntraOptions.IsIdentifier(identity.TenantId) && EntraOptions.IsIdentifier(identity.ClientId))
            {
                var issuer = $"https://login.microsoftonline.com/{identity.TenantId}/v2.0";
                options.Authority = issuer;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    // Discovery may supply an issuer too; never broaden the configured tenant.
                    IssuerValidator = (value, _, _) => value == issuer ? issuer
                        : throw new SecurityTokenInvalidIssuerException("Invalid issuer."),
                    ValidateAudience = true,
                    ValidAudience = identity.ClientId,
                    IgnoreTrailingSlashWhenValidatingAudience = false,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ClockSkew = TimeSpan.FromSeconds(60),
                    NameClaimType = "oid",
                    RoleClaimType = "roles"
                };
            }
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // An unprovisioned host never accepts an identity or calls discovery.
                    if (!identity.Enabled) context.NoResult();
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var user = context.Principal!;
                    var delegated = !string.IsNullOrWhiteSpace(user.FindFirst("scp")?.Value);
                    var application = user.FindFirst("idtyp")?.Value == "app"
                        && user.FindAll("roles").Any(c => !string.IsNullOrWhiteSpace(c.Value));
                    if (user.FindFirst("tid")?.Value != identity.TenantId
                        || user.FindFirst("ver")?.Value != "2.0"
                        || !EntraOptions.IsIdentifier(user.FindFirst("oid")?.Value ?? "")
                        || !EntraOptions.IsIdentifier(user.FindFirst("azp")?.Value ?? "")
                        || !(user.FindFirst("idtyp")?.Value == "app" ? application && !delegated : delegated))
                        context.Fail("Invalid access-token identity.");
                    return Task.CompletedTask;
                }
            };
        });
        return services;
    }
}
