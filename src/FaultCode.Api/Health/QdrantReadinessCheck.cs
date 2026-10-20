using FaultCode.Api.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FaultCode.Api.Health;

public sealed class QdrantReadinessCheck(HttpClient client, IOptions<QdrantOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return HealthCheckResult.Healthy();
        }
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            using var request = new HttpRequestMessage(HttpMethod.Get,
                new Uri(new Uri(options.Value.Endpoint), "/collections"));
            request.Headers.Add("api-key", options.Value.ApiKey);
            using var response = await client.SendAsync(request, timeout.Token);
            return response.IsSuccessStatusCode ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Qdrant is unavailable.");
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Qdrant is unavailable.");
        }
    }
}
