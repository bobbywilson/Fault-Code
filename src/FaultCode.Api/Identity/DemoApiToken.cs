using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography.X509Certificates;
using FaultCode.Api.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;

namespace FaultCode.Api.Identity;

public sealed class DemoApiTokenOptions
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = "";
    public string CertificatePath { get; set; } = "";
    public string PrivateKeyPath { get; set; } = "";
}

public sealed record DemoApiTokenResult(string AccessToken, DateTimeOffset ExpiresAt, string ClientId,
    string IdentityName = "Fault Code Demo API");

public interface IDemoApiTokenProvider
{
    Task<DemoApiTokenResult> AcquireAsync(bool refresh, CancellationToken cancellationToken);
}

// This is an Entra confidential application, never a local issuer or a user token.
// MSAL holds its cache in server memory; the certificate key never enters a response.
public sealed class DemoApiTokenProvider(IOptions<DemoApiTokenOptions> configured,
    IOptions<EntraOptions> entra, IOptionsMonitor<JwtBearerOptions> bearer) : IDemoApiTokenProvider, IDisposable
{
    public static readonly string[] RequiredRoles =
        ["Drivers.Read.All", "Faults.Read.All", "Faults.Write.All", "Knowledge.Read.All", "Vehicles.Read.All"];
    private readonly SemaphoreSlim gate = new(1, 1);
    private IConfidentialClientApplication? client;
    private X509Certificate2? certificate;

    public async Task<DemoApiTokenResult> AcquireAsync(bool refresh, CancellationToken cancellationToken)
    {
        var options = configured.Value;
        var identity = entra.Value;
        if (!options.Enabled || !identity.Enabled) throw new InvalidOperationException("Demo API identity is not configured.");
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (client is null)
            {
                certificate = X509Certificate2.CreateFromPemFile(options.CertificatePath, options.PrivateKeyPath);
                client = ConfidentialClientApplicationBuilder.Create(options.ClientId)
                    .WithAuthority($"https://login.microsoftonline.com/{identity.TenantId}")
                    .WithCertificate(certificate).Build();
            }
            var result = await client.AcquireTokenForClient([$"api://{identity.ClientId}/.default"])
                .WithForceRefresh(refresh).ExecuteAsync(cancellationToken);
            // Reject accidental role expansion or a token for any other principal/resource.
            // Use the same trusted discovery/signature parameters as the receiving API.
            var jwt = bearer.Get(JwtBearerDefaults.AuthenticationScheme);
            var discovery = await jwt.ConfigurationManager!.GetConfigurationAsync(cancellationToken);
            var parameters = jwt.TokenValidationParameters.Clone();
            parameters.IssuerSigningKeys = discovery.SigningKeys;
            var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }
                .ValidateToken(result.AccessToken, parameters, out _);
            if (principal.FindFirst("azp")?.Value != options.ClientId
                || principal.FindFirst("tid")?.Value != identity.TenantId
                || principal.FindFirst("ver")?.Value != "2.0"
                || principal.FindFirst("idtyp")?.Value != "app"
                || principal.HasClaim(c => c.Type == "scp")
                || !principal.FindAll("roles").Select(c => c.Value).Order(StringComparer.Ordinal)
                    .SequenceEqual(RequiredRoles, StringComparer.Ordinal))
                throw new InvalidOperationException("Unexpected demo API identity grants.");
            return new(result.AccessToken, result.ExpiresOn, options.ClientId);
        }
        finally { gate.Release(); }
    }

    public void Dispose() { certificate?.Dispose(); gate.Dispose(); }
}
