using FaultCode.Application.Telemetry;

namespace FaultCode.Infrastructure.Telemetry;

// Explicit outbound attempt instrumentation, without collecting endpoint, method, headers or content.
public sealed class SanitizedHttpTelemetryHandler(ITelemetry telemetry) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var scope = telemetry.Begin(TelemetryOperation.Http);
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) scope.Report(TelemetryError.Dependency);
            scope.Complete(response.IsSuccessStatusCode ? TelemetryOutcome.Success : TelemetryOutcome.Failed);
            return response;
        }
        catch (OperationCanceledException) { scope.Report(TelemetryError.Cancelled); scope.Complete(TelemetryOutcome.Cancelled); throw; }
        catch { scope.Report(TelemetryError.Dependency); throw; }
    }
}
