using FaultCode.Infrastructure.Telemetry;

namespace FaultCode.Api.Configuration;

public sealed class TelemetryOptions
{
    public string Provider { get; set; } = "None";
    public string Endpoint { get; set; } = "";
    public double TraceSampleRatio { get; set; } = 0.1;
    public bool Valid => Provider is "None" or "Local" || Provider is "Otlp" or "Datadog"
        && new OtlpTelemetrySettings(Endpoint, TraceSampleRatio).Valid
        && (Provider != "Datadog" || Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) && uri.IsLoopback);
}
