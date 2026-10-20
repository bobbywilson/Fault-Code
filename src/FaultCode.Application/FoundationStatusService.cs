namespace FaultCode.Application;

public sealed class FoundationStatusService(string applicationName, IRuntimeEnvironment environment)
{
    public FoundationStatus GetStatus() => new(applicationName, environment.Name);
}
