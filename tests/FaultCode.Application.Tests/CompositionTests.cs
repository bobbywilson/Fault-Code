using FaultCode.Application;
using FaultCode.Infrastructure;

namespace FaultCode.Application.Tests;

public sealed class CompositionTests
{
    [Fact]
    public void Infrastructure_environment_is_available_without_credentials()
    {
        var service = new FoundationStatusService("Fault Code", new RuntimeEnvironment("Development"));
        Assert.Equal(new FoundationStatus("Fault Code", "Development"), service.GetStatus());
    }

    [Fact]
    public void Application_accepts_a_replacement_adapter_without_infrastructure_setup()
    {
        var service = new FoundationStatusService("Demo test", new TestEnvironment());
        Assert.Equal(new FoundationStatus("Demo test", "Isolated"), service.GetStatus());
    }

    private sealed class TestEnvironment : IRuntimeEnvironment
    {
        public string Name => "Isolated";
    }
}
