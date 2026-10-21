namespace FaultCode.Application.Telemetry;

public enum TelemetryOperation { Api, Mcp, Incident, Retrieval, Investigation, Model, Agent, Engineering, Notification, Http, Delivery }
public enum TelemetryOutcome { Success, Rejected, Failed, Cancelled }
public enum TelemetryError { Dependency, InvalidInput, Unexpected, Cancelled }
public sealed record TelemetryContext(string CorrelationId, string TraceId, string SpanId, string? ParentSpanId);
public sealed record TelemetryRecord(TelemetryOperation Operation, TelemetryOutcome Outcome, TelemetryContext Context,
    double DurationMilliseconds, long? InputTokens = null, long? OutputTokens = null, TelemetryError? Error = null);

// Adapters must be bounded in-process notifications: no blocking I/O or retries; exporters may enqueue into bounded SDK buffers.
// Only sanitized records reach this boundary. No raw exceptions or arbitrary property bags.
public interface ITelemetryAdapter { void Record(TelemetryRecord record); }
public interface ITelemetry
{
    ITelemetryScope Begin(TelemetryOperation operation, string? correlationId = null);
}
public interface ITelemetryScope : IDisposable
{
    void Complete(TelemetryOutcome outcome, long? inputTokens = null, long? outputTokens = null);
    void Report(TelemetryError error);
}

public static class TelemetryWork
{
    public static async Task<T> Run<T>(this ITelemetry telemetry, TelemetryOperation operation, string correlation,
        Func<Task<T>> work)
    {
        using var scope = telemetry.Begin(operation, correlation);
        try { var result = await work(); scope.Complete(TelemetryOutcome.Success); return result; }
        catch (OperationCanceledException) { scope.Report(TelemetryError.Cancelled); scope.Complete(TelemetryOutcome.Cancelled); throw; }
        catch { scope.Report(TelemetryError.Dependency); scope.Complete(TelemetryOutcome.Failed); throw; }
    }
}

public sealed class NullTelemetry : ITelemetry
{
    public static readonly NullTelemetry Instance = new();
    public ITelemetryScope Begin(TelemetryOperation operation, string? correlationId = null) => EmptyScope.Instance;
    private sealed class EmptyScope : ITelemetryScope
    {
        public static readonly EmptyScope Instance = new();
        public void Complete(TelemetryOutcome outcome, long? inputTokens = null, long? outputTokens = null) { }
        public void Report(TelemetryError error) { }
        public void Dispose() { }
    }
}
