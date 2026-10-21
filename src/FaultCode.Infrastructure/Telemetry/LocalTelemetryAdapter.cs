using System.Diagnostics;
using System.Diagnostics.Metrics;
using FaultCode.Application.Telemetry;
using Microsoft.Extensions.Logging;

namespace FaultCode.Infrastructure.Telemetry;

// No exporter, network client, raw exception capture or unbounded buffer.
public sealed class LocalTelemetryAdapter(ILogger<LocalTelemetryAdapter> logger) : ITelemetryAdapter
{
    public const string SourceName = SafeTelemetry.SourceName;
    private static readonly Meter Meter = new(SourceName, "1.0.0");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("faultcode.operation.duration", "ms");
    private static readonly Counter<long> Input = Meter.CreateCounter<long>("faultcode.model.input_tokens");
    private static readonly Counter<long> Output = Meter.CreateCounter<long>("faultcode.model.output_tokens");
    public void Record(TelemetryRecord r)
    {
        var tags = new TagList { { "operation", r.Operation.ToString() }, { "outcome", r.Outcome.ToString() } };
        Duration.Record(r.DurationMilliseconds, tags);
        if (r.InputTokens is { } input) Input.Add(input, tags);
        if (r.OutputTokens is { } output) Output.Add(output, tags);
        logger.LogInformation("Operation {Operation} outcome {Outcome} durationMs {DurationMs} correlationId {CorrelationId} traceId {TraceId} spanId {SpanId} parentSpanId {ParentSpanId} error {Error} inputTokens {InputTokens} outputTokens {OutputTokens}",
            r.Operation, r.Outcome, r.DurationMilliseconds, r.Context.CorrelationId, r.Context.TraceId, r.Context.SpanId,
            r.Context.ParentSpanId, r.Error, r.InputTokens, r.OutputTokens);
    }
}
