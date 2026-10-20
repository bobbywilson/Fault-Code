using System.Net;
using FaultCode.Api.Configuration;
using FaultCode.Application;
using FaultCode.Application.Persistence;
using FaultCode.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
        Assert.Equal("Fault Code", factory.Services.GetRequiredService<FoundationStatusService>().GetStatus().ApplicationName);
    }

    [Fact]
    public async Task Unimplemented_business_routes_expose_no_data()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/vehicles")).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_or_blank_configuration_prevents_host_startup(string? name)
    {
        using var factory = WithName(name);
        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("FaultCode:ApplicationName", error.Message);
        Assert.Contains("set FaultCode__ApplicationName", error.Message);
    }

    [Fact]
    public void Invalid_configuration_diagnostics_do_not_disclose_the_value()
    {
        var suppliedValue = new string('s', 101);
        using var factory = WithName(suppliedValue);
        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.DoesNotContain(suppliedValue, error.ToString());
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

    private sealed record SyntheticAggregate(Guid Id) : IAggregateRoot;

    private static WebApplicationFactory<Program> WithName(string? name) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{FoundationOptions.SectionName}:ApplicationName"] = name
            })));

    private sealed class TestEnvironment : IRuntimeEnvironment
    {
        public string Name => "Adapter test";
    }
}
