using System.Net;
using System.Diagnostics;
using FaultCode.Application;
using FaultCode.Application.Persistence;
using FaultCode.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace FaultCode.IntegrationTests;

public sealed class ApiHostTests
{
    [Fact]
    public async Task Liveness_reports_process_availability_without_provider_credentials()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        using var domainRequest = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        domainRequest.Headers.Host = "faultcode.test";
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(domainRequest)).StatusCode);
        Assert.Equal("Fault Code", factory.Services.GetRequiredService<FoundationStatusService>().GetStatus().ApplicationName);
    }

    [Fact]
    public async Task Vehicle_routes_require_identity()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/vehicles")).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_or_blank_configuration_prevents_host_startup(string? name)
    {
        var (exitCode, output) = await StartWithInvalidName(name);
        Assert.NotEqual(0, exitCode);
        Assert.Contains("FaultCode:ApplicationName", output);
        Assert.Contains("set FaultCode__ApplicationName", output);
    }

    [Fact]
    public async Task Invalid_configuration_diagnostics_do_not_disclose_the_value()
    {
        var suppliedValue = new string('s', 101);
        var (exitCode, output) = await StartWithInvalidName(suppliedValue);
        Assert.NotEqual(0, exitCode);
        Assert.Contains("FaultCode:ApplicationName", output);
        Assert.DoesNotContain(suppliedValue, output);
    }

    [Fact]
    public void Environment_variable_overrides_json_and_reaches_the_composed_service()
    {
        const string key = "FaultCode__ApplicationName";
        var previous = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, "Synthetic environment override");
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            Assert.Equal("Synthetic environment override", factory.Services.GetRequiredService<FoundationStatusService>()
                .GetStatus().ApplicationName);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
        }
    }

    [Fact]
    public void Host_composition_accepts_a_replacement_adapter()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton<IRuntimeEnvironment>(new TestEnvironment()))));
        using var client = factory.CreateClient();
        Assert.Equal("Adapter test", factory.Services.GetRequiredService<FoundationStatusService>().GetStatus().EnvironmentName);
    }

    [Fact]
    public void Foundation_does_not_silently_register_a_persistence_provider()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        Assert.Null(factory.Services.GetService<IUnitOfWork>());
        Assert.Null(factory.Services.GetService<IRepository<SyntheticAggregate>>());
    }

    [Fact]
    public async Task Invalid_telemetry_provider_prevents_startup_without_echoing_value()
    {
        const string secret = "SECRET-contact-token";
        var (code, output) = await StartWithInvalidName("Fault Code", secret);
        Assert.NotEqual(0, code); Assert.Contains("Telemetry:Provider", output); Assert.DoesNotContain(secret, output);
    }

    [Theory]
    [InlineData("https://user:SECRET@example.test", "1")]
    [InlineData("http://127.0.0.1:4318", "SECRET-not-a-number")]
    [InlineData("http://127.0.0.1:4318", "NaN")]
    public async Task Invalid_OTLP_settings_fail_real_startup_without_echoing_values(string endpoint, string ratio)
    {
        var (code, output) = await StartWithInvalidName("Fault Code", "Otlp", endpoint, ratio);
        Assert.NotEqual(0, code); Assert.Contains("Telemetry:Provider, Endpoint or TraceSampleRatio", output);
        Assert.DoesNotContain("SECRET", output);
    }

    private sealed record SyntheticAggregate(Guid Id) : IAggregateRoot;

    // Run invalid startup out of process: WebApplicationFactory can dispose its deferred
    // service provider before surfacing the original validation exception on fast failures.
    private static async Task<(int ExitCode, string Output)> StartWithInvalidName(string? name, string? telemetry = null, string? endpoint = null, string? ratio = null)
    {
        var directory = Directory.CreateTempSubdirectory("faultcode-startup-");
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
        if (telemetry is not null) process.StartInfo.Environment["Telemetry__Provider"] = telemetry;
        if (endpoint is not null) process.StartInfo.Environment["Telemetry__Endpoint"] = endpoint;
        if (ratio is not null) process.StartInfo.Environment["Telemetry__TraceSampleRatio"] = ratio;
        process.StartInfo.Environment.Remove("FaultCode__ApplicationName");
        process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        process.StartInfo.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        if (name is not null)
        {
            process.StartInfo.Environment["FaultCode__ApplicationName"] = name;
        }
        try
        {
            Assert.True(process.Start());
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await process.WaitForExitAsync(timeout.Token);
            return (process.ExitCode, await stdout + await stderr);
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

    private sealed class TestEnvironment : IRuntimeEnvironment
    {
        public string Name => "Adapter test";
    }
}
