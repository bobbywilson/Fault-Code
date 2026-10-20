using System.Net;
using FaultCode.Api.Configuration;
using FaultCode.Api.Health;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FaultCode.IntegrationTests;

public sealed class ReadinessTests
{
    [Fact]
    public async Task Native_foundation_readiness_needs_no_provider()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, HealthStatus.Healthy)]
    [InlineData(HttpStatusCode.Unauthorized, HealthStatus.Unhealthy)]
    [InlineData(HttpStatusCode.ServiceUnavailable, HealthStatus.Unhealthy)]
    public async Task Probe_authenticates_without_disclosing_credentials(HttpStatusCode code, HealthStatus expected)
    {
        var handler = new ProbeHandler(code);
        using var client = new HttpClient(handler);
        var probe = new QdrantReadinessCheck(client, Options.Create(new QdrantOptions
        {
            Enabled = true,
            Endpoint = "http://qdrant:6333",
            ApiKey = "synthetic-probe-key"
        }));
        var result = await probe.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(expected, result.Status);
        Assert.Equal("http://qdrant:6333/collections", handler.Url);
        Assert.Equal("synthetic-probe-key", handler.Key);
        Assert.DoesNotContain("synthetic-probe-key", result.Description ?? "");
        Assert.Null(result.Exception);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transport_failure_and_cancellation_are_generic(bool cancel)
    {
        using var client = new HttpClient(new FailureHandler(cancel));
        var probe = new QdrantReadinessCheck(client, Options.Create(new QdrantOptions
        { Enabled = true, ApiKey = "synthetic-key" }));
        var result = await probe.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Qdrant is unavailable.", result.Description);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task Failed_dependency_changes_readiness_but_not_liveness()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Qdrant:Enabled"] = "true",
                ["Qdrant:ApiKey"] = "synthetic-key"
            }));
            builder.ConfigureServices(services => services.AddHttpClient<QdrantReadinessCheck>()
                .ConfigurePrimaryHttpMessageHandler(() => new ProbeHandler(HttpStatusCode.Unauthorized)));
        });
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }

    [Theory]
    [InlineData(null, "http://qdrant:6333", "Qdrant:ApiKey")]
    [InlineData("", "http://qdrant:6333", "Qdrant:ApiKey")]
    [InlineData("synthetic-key", "not-a-url", "Qdrant:Endpoint")]
    [InlineData("synthetic-key", "http://user:secret@qdrant:6333", "Qdrant:Endpoint")]
    public void Enabled_configuration_rejects_missing_key_and_invalid_endpoint(string? key, string endpoint, string setting)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Qdrant:Enabled"] = "true",
                ["Qdrant:ApiKey"] = key,
                ["Qdrant:Endpoint"] = endpoint
            })));
        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains(setting, exception.Message);
        Assert.DoesNotContain("synthetic-key", exception.Message);
        Assert.DoesNotContain("user:secret", exception.Message);
    }

    private sealed class ProbeHandler(HttpStatusCode code) : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? Key { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri!.ToString();
            Key = request.Headers.GetValues("api-key").Single();
            return Task.FromResult(new HttpResponseMessage(code));
        }
    }

    private sealed class FailureHandler(bool cancel) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw (cancel ? new OperationCanceledException("synthetic-key") : new HttpRequestException("synthetic-key"));
    }
}
