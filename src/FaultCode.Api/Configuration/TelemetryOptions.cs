namespace FaultCode.Api.Configuration;

public sealed class TelemetryOptions
{
    public string Provider { get; set; } = "None";
    public bool Valid => Provider is "None" or "Local";
}
