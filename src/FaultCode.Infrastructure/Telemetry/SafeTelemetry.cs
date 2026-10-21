using System.Diagnostics;
using FaultCode.Application.Telemetry;

namespace FaultCode.Infrastructure.Telemetry;

// The facade owns correlation independently of collector availability.
public sealed class SafeTelemetry(ITelemetryAdapter adapter) : ITelemetry
{
    public const string SourceName = "FaultCode.Operations";
    private static readonly ActivitySource Source = new(SourceName, "1.0.0");
    private readonly AsyncLocal<TelemetryContext?> current = new();
    public ITelemetryScope Begin(TelemetryOperation operation, string? correlationId = null)
    {
        if (adapter is NoOpTelemetryAdapter || !Enum.IsDefined(operation)) return NullTelemetry.Instance.Begin(operation, correlationId);
        var parent = current.Value;
        var correlation = parent?.CorrelationId ?? (Guid.TryParseExact(correlationId, "N", out var id) && id != Guid.Empty
            ? id.ToString("N") : Guid.NewGuid().ToString("N"));
        var priorActivity = Activity.Current;
        Activity? activity = null;
        try
        {
            // Do not inherit an untrusted incoming HTTP trace as application correlation.
            Activity.Current = null;
            var pc = parent is null ? default : new ActivityContext(ActivityTraceId.CreateFromString(parent.TraceId),
                ActivitySpanId.CreateFromString(parent.SpanId), ActivityTraceFlags.Recorded);
            activity = Source.StartActivity(Enum.IsDefined(operation) ? operation.ToString() : "Invalid", ActivityKind.Internal, pc);
        }
        catch (Exception)
        {
            // Start callbacks may throw after setting Current; retire that partial span too.
            try { Activity.Current?.Dispose(); } catch (Exception) { }
            Activity.Current = null;
        }
        if (activity is null)
        {
            activity = new Activity("FaultCode.Operation").SetIdFormat(ActivityIdFormat.W3C);
            if (parent is not null) activity.SetParentId(ActivityTraceId.CreateFromString(parent.TraceId), ActivitySpanId.CreateFromString(parent.SpanId));
            activity.Start();
        }
        var context = new TelemetryContext(correlation, activity.TraceId.ToString(), activity.SpanId.ToString(), parent?.SpanId);
        current.Value = context;
        return new Scope(this, operation, context, parent, activity, priorActivity);
    }
    private void Record(TelemetryRecord record) => adapter.Record(record);
    private sealed class Scope(SafeTelemetry owner, TelemetryOperation operation, TelemetryContext context,
        TelemetryContext? parent, Activity activity, Activity? priorActivity) : ITelemetryScope
    {
        private readonly long started = Stopwatch.GetTimestamp();
        private TelemetryOutcome outcome = TelemetryOutcome.Failed;
        private TelemetryError? error;
        private long? input, output;
        private bool disposed, valid = Enum.IsDefined(operation);
        public void Complete(TelemetryOutcome value, long? inputTokens = null, long? outputTokens = null)
        {
            valid &= Enum.IsDefined(value) && Valid(inputTokens) && Valid(outputTokens);
            outcome = value; input = inputTokens; output = outputTokens;
        }
        public void Report(TelemetryError value) { valid &= Enum.IsDefined(value); error = value; }
        private static bool Valid(long? value) => value is null or (>= 0 and <= 1_000_000_000);
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            owner.current.Value = parent;
            if (!valid)
            {
                try { activity.Dispose(); } catch (Exception) { }
                Activity.Current = priorActivity; return;
            }
            var elapsed = Math.Clamp(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 0, 86_400_000);
            try
            {
                activity.SetTag("faultcode.correlation_id", context.CorrelationId);
                activity.SetTag("faultcode.outcome", outcome.ToString());
                if (error is not null) activity.SetTag("error.type", error.ToString());
                if (input is not null) activity.SetTag("gen_ai.usage.input_tokens", input);
                if (output is not null) activity.SetTag("gen_ai.usage.output_tokens", output);
                activity.SetStatus(outcome == TelemetryOutcome.Failed ? ActivityStatusCode.Error : ActivityStatusCode.Unset);
                owner.Record(new(operation, outcome, context, elapsed, input, output, error));
            }
            catch (Exception) { /* Telemetry never changes business completion, including cancellation. */ }
            finally
            {
                try { activity.Dispose(); } catch (Exception) { }
                Activity.Current = priorActivity;
            }
        }
    }
}

public sealed class NoOpTelemetryAdapter : ITelemetryAdapter
{
    public void Record(TelemetryRecord record) { }
}
