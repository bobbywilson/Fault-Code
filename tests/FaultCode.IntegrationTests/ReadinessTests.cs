using System.Diagnostics;
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
            ApiKey = "demo-probe-key"
        }));
        var result = await probe.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(expected, result.Status);
        Assert.Equal("http://qdrant:6333/collections", handler.Url);
        Assert.Equal("demo-probe-key", handler.Key);
        Assert.DoesNotContain("demo-probe-key", result.Description ?? "");
        Assert.Null(result.Exception);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transport_failure_and_cancellation_are_generic(bool cancel)
    {
        using var client = new HttpClient(new FailureHandler(cancel));
        var probe = new QdrantReadinessCheck(client, Options.Create(new QdrantOptions
        { Enabled = true, ApiKey = "demo-key" }));
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
                ["Qdrant:ApiKey"] = "demo-key"
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
    [InlineData("demo-key", "not-a-url", "Qdrant:Endpoint")]
    [InlineData("demo-key", "http://user:secret@qdrant:6333", "Qdrant:Endpoint")]
    public async Task Enabled_configuration_rejects_missing_key_and_invalid_endpoint(string? key, string endpoint, string setting)
    {
        var directory = Directory.CreateTempSubdirectory("faultcode-qdrant-startup-");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
                WorkingDirectory = directory.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        process.StartInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
        process.StartInfo.Environment["FaultCode__ApplicationName"] = "Demo startup test";
        process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        process.StartInfo.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        process.StartInfo.Environment["Qdrant__Enabled"] = "true";
        process.StartInfo.Environment["Qdrant__Endpoint"] = endpoint;
        process.StartInfo.Environment.Remove("Qdrant__ApiKey");
        if (key is not null)
        {
            process.StartInfo.Environment["Qdrant__ApiKey"] = key;
        }
        try
        {
            // An actual process avoids WebApplicationFactory's deferred-host disposal race
            // when startup validation fails before the test server is fully created.
            Assert.True(process.Start());
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains(setting, output);
            Assert.DoesNotContain("demo-key", output);
            Assert.DoesNotContain("user:secret", output);
        }
        finally
        {
            if (process.Id != 0 && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            directory.Delete(recursive: true);
        }
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
            => throw (cancel ? new OperationCanceledException("demo-key") : new HttpRequestException("demo-key"));
    }
}
