using System.Diagnostics;
using FaultCode.Application.Telemetry;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FaultCode.Infrastructure.Telemetry;

// A dedicated SDK pipeline: no global ILogger provider, automatic HTTP capture or environment resource detectors.
public sealed class OtlpTelemetryAdapter : ITelemetryAdapter, IDisposable
{
    public const int QueueCapacity = 512;
    public const int BatchSize = 128;
    public const int ExportTimeoutMilliseconds = 2000;
    private readonly TracerProvider traces;
    private readonly BatchActivityExportProcessor traceProcessor;
    private readonly BatchLogRecordExportProcessor logProcessor;
    private readonly MeterProvider metrics;
    private readonly ILoggerFactory logs;
    private readonly LocalTelemetryAdapter projection;
    private readonly string? model;
    private int disposed;
    public OtlpTelemetryAdapter(OtlpTelemetrySettings settings)
    {
        if (!settings.Valid) throw new ArgumentException("Telemetry configuration is invalid.");
        model = settings.Model;
        ResourceBuilder Resource() => ResourceBuilder.CreateEmpty().AddService("fault-code", serviceVersion: "1.0.0", autoGenerateServiceInstanceId: false);
        OtlpExporterOptions Options(string signal) => new()
        {
            Endpoint = new Uri(new Uri(settings.Endpoint), "v1/" + signal),
            Protocol = OtlpExportProtocol.HttpProtobuf,
            Headers = "",
            TimeoutMilliseconds = ExportTimeoutMilliseconds,
            HttpClientFactory = () => new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false })
            { Timeout = TimeSpan.FromMilliseconds(ExportTimeoutMilliseconds) }
        };
        traceProcessor = new BatchActivityExportProcessor(new OtlpTraceExporter(Options("traces")),
            QueueCapacity, 1000, ExportTimeoutMilliseconds, BatchSize);
        logProcessor = new BatchLogRecordExportProcessor(new OtlpLogExporter(Options("logs")),
            QueueCapacity, 1000, ExportTimeoutMilliseconds, BatchSize);
        traces = Sdk.CreateTracerProviderBuilder().SetResourceBuilder(Resource())
            .AddSource(SafeTelemetry.SourceName)
            .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(settings.TraceSampleRatio)))
            .AddProcessor(traceProcessor).Build();
        metrics = Sdk.CreateMeterProviderBuilder().SetResourceBuilder(Resource())
            .AddMeter(SafeTelemetry.SourceName)
            .AddView("*", new MetricStreamConfiguration { TagKeys = ["operation", "outcome"], CardinalityLimit = 64 })
            .AddReader(new PeriodicExportingMetricReader(new OtlpMetricExporter(Options("metrics")), 5000, ExportTimeoutMilliseconds)).Build();
        logs = LoggerFactory.Create(b => b.AddOpenTelemetry(o =>
        {
            o.SetResourceBuilder(Resource());
            o.IncludeScopes = false; o.IncludeFormattedMessage = false; o.ParseStateValues = true;
            o.AddProcessor(logProcessor);
        }));
        projection = new LocalTelemetryAdapter(logs.CreateLogger<LocalTelemetryAdapter>());
    }
    public void Record(TelemetryRecord record)
    {
        if (Volatile.Read(ref disposed) != 0) return;
        if (record.Operation == TelemetryOperation.Model && model is not null)
            Activity.Current?.SetTag("gen_ai.request.model", model);
        projection.Record(record);
    }
    // Only tests/operator shutdown use flushing; never call this on a request path.
    public bool ForceFlush() => traces.ForceFlush(ExportTimeoutMilliseconds)
        & metrics.ForceFlush(ExportTimeoutMilliseconds) & logProcessor.ForceFlush(ExportTimeoutMilliseconds);
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        try { traces.Shutdown(ExportTimeoutMilliseconds); traces.Dispose(); } catch (Exception) { }
        try { metrics.Shutdown(ExportTimeoutMilliseconds); metrics.Dispose(); } catch (Exception) { }
        try { logProcessor.Shutdown(ExportTimeoutMilliseconds); logs.Dispose(); } catch (Exception) { }
    }
}
