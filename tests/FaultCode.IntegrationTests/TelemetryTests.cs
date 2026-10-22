using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using FaultCode.Application.Telemetry;
using FaultCode.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace FaultCode.IntegrationTests;

public sealed class TelemetryTests
{
    private sealed class Collector : ITelemetryAdapter
    {
        public readonly ConcurrentBag<TelemetryRecord> Records = [];
        public void Record(TelemetryRecord record) => Records.Add(record);
    }
    private sealed class Broken : ITelemetryAdapter
    {
        public void Record(TelemetryRecord record) => throw new InvalidOperationException("SECRET contact prompt token");
    }
    private sealed class LogCollector : ILogger<LocalTelemetryAdapter>
    {
        public readonly List<string> Lines = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));
    }
    private static ActivityListener Listen(Action<Activity>? stop = null) => new()
    {
        ShouldListenTo = s => s.Name == SafeTelemetry.SourceName,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        ActivityStopped = stop
    };

    [Fact]
    public async Task Nested_concurrent_operations_preserve_parentage_and_partial_usage()
    {
        var collector = new Collector(); var telemetry = new SafeTelemetry(collector);
        var correlations = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid().ToString("N")).ToArray();
        await Task.WhenAll(correlations.Select(async correlation =>
        {
            using var root = telemetry.Begin(TelemetryOperation.Api, correlation);
            await Task.Yield();
            using (var model = telemetry.Begin(TelemetryOperation.Model, "spoofed"))
            { await Task.Delay(1); model.Complete(TelemetryOutcome.Success, 23, null); }
            root.Complete(TelemetryOutcome.Success);
        }));
        Assert.Equal(40, collector.Records.Count);
        foreach (var correlation in correlations)
        {
            var pair = collector.Records.Where(x => x.Context.CorrelationId == correlation).ToArray();
            var root = Assert.Single(pair, x => x.Operation == TelemetryOperation.Api);
            var child = Assert.Single(pair, x => x.Operation == TelemetryOperation.Model);
            Assert.Null(root.Context.ParentSpanId); Assert.Equal(root.Context.TraceId, child.Context.TraceId);
            Assert.Equal(root.Context.SpanId, child.Context.ParentSpanId); Assert.NotEqual(root.Context.SpanId, child.Context.SpanId);
            Assert.Equal(23, child.InputTokens); Assert.Null(child.OutputTokens); Assert.Null(root.InputTokens);
        }
        Assert.Null(Activity.Current);
    }

    [Theory]
    [InlineData("token=SECRET@example.com")]
    [InlineData("")]
    [InlineData("00000000000000000000000000000000")]
    public void Invalid_correlation_is_replaced_and_no_arbitrary_data_enters_records(string correlation)
    {
        var collector = new Collector(); var telemetry = new SafeTelemetry(collector);
        using (var scope = telemetry.Begin(TelemetryOperation.Api, correlation)) { scope.Report(TelemetryError.Dependency); }
        var record = Assert.Single(collector.Records);
        Assert.True(Guid.TryParseExact(record.Context.CorrelationId, "N", out var id)); Assert.NotEqual(Guid.Empty, id);
        Assert.DoesNotContain("SECRET", JsonSerializer.Serialize(record)); Assert.InRange(record.DurationMilliseconds, 0, 86_400_000);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_000_001)]
    public void Invalid_usage_is_not_exported(long value)
    {
        var collector = new Collector(); var telemetry = new SafeTelemetry(collector);
        using (var scope = telemetry.Begin(TelemetryOperation.Model)) scope.Complete(TelemetryOutcome.Success, value);
        Assert.Empty(collector.Records);
    }

    [Fact]
    public void Invalid_enums_and_repeated_disposal_do_not_report_unbounded_values()
    {
        var collector = new Collector(); var telemetry = new SafeTelemetry(collector);
        using (var scope = telemetry.Begin((TelemetryOperation)999)) scope.Complete(TelemetryOutcome.Success);
        using (var scope = telemetry.Begin(TelemetryOperation.Api)) scope.Report((TelemetryError)999);
        using (var scope = telemetry.Begin(TelemetryOperation.Api)) scope.Complete((TelemetryOutcome)999);
        Assert.Empty(collector.Records);
        var good = telemetry.Begin(TelemetryOperation.Api); good.Complete(TelemetryOutcome.Success); good.Dispose(); good.Dispose();
        Assert.Single(collector.Records);
    }

    [Fact]
    public async Task Collector_outage_preserves_success_original_error_and_cancellation()
    {
        var telemetry = new SafeTelemetry(new Broken());
        Assert.Equal(42, await telemetry.Run(TelemetryOperation.Incident, "", () => Task.FromResult(42)));
        var expected = new InvalidOperationException("private body");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => telemetry.Run<int>(TelemetryOperation.Investigation, "", () => throw expected));
        Assert.Same(expected, actual);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => telemetry.Run<int>(TelemetryOperation.Model, "", () => Task.FromCanceled<int>(cancel.Token)));
        Assert.Equal(cancel.Token, error.CancellationToken);
        Assert.Equal(43, await telemetry.Run(TelemetryOperation.Incident, "", () => Task.FromResult(43)));
        Assert.Null(Activity.Current);
    }

    [Fact]
    public void Noop_emits_no_traces_or_metrics()
    {
        var traces = new List<Activity>(); using var listener = Listen(traces.Add); ActivitySource.AddActivityListener(listener);
        var telemetry = new SafeTelemetry(new NoOpTelemetryAdapter());
        using (var scope = telemetry.Begin(TelemetryOperation.Api)) scope.Complete(TelemetryOutcome.Success);
        Assert.Empty(traces); Assert.Null(Activity.Current);
    }

    [Fact]
    public void Local_traces_logs_metrics_share_exact_context_and_metrics_exclude_identifiers()
    {
        var traces = new List<Activity>(); using var listener = Listen(traces.Add); ActivitySource.AddActivityListener(listener);
        var measurements = new List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)>();
        using var meter = new MeterListener();
        meter.InstrumentPublished = (i, l) => { if (i.Meter.Name == SafeTelemetry.SourceName) l.EnableMeasurementEvents(i); };
        meter.SetMeasurementEventCallback<double>((i, v, tags, _) => measurements.Add((i.Name, v, tags.ToArray())));
        meter.SetMeasurementEventCallback<long>((i, v, tags, _) => measurements.Add((i.Name, v, tags.ToArray()))); meter.Start();
        var logs = new LogCollector(); var telemetry = new SafeTelemetry(new LocalTelemetryAdapter(logs)); var correlation = Guid.NewGuid().ToString("N");
        using (var api = telemetry.Begin(TelemetryOperation.Api, correlation))
        {
            using (var model = telemetry.Begin(TelemetryOperation.Model)) model.Complete(TelemetryOutcome.Success, 12, null);
            api.Complete(TelemetryOutcome.Success);
        }
        Assert.Equal(2, traces.Count); var parent = Assert.Single(traces, x => x.OperationName == "Api");
        var child = Assert.Single(traces, x => x.OperationName == "Model");
        Assert.Equal(parent.SpanId, child.ParentSpanId); Assert.Equal(parent.TraceId, child.TraceId);
        Assert.All(traces, trace =>
        {
            Assert.Equal(correlation, trace.GetTagItem("faultcode.correlation_id"));
            Assert.Contains(logs.Lines, line => line.Contains(trace.TraceId.ToString()) && line.Contains(trace.SpanId.ToString()) && line.Contains(correlation));
            Assert.Null(trace.GetTagItem("gen_ai.usage.output_tokens"));
        });
        Assert.Equal(3, measurements.Count); Assert.Equal(12, Assert.Single(measurements, x => x.Name.EndsWith("input_tokens", StringComparison.Ordinal)).Value);
        Assert.All(measurements, x => Assert.Equal(new[] { "operation", "outcome" }, x.Tags.Select(t => t.Key)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sampler_and_stop_listener_failures_do_not_escape(bool atStop)
    {
        using var listener = Listen();
        if (atStop) listener.ActivityStopped = _ => throw new InvalidOperationException("SECRET");
        else listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => throw new InvalidOperationException("SECRET");
        ActivitySource.AddActivityListener(listener);
        var collector = new Collector(); var telemetry = new SafeTelemetry(collector);
        using (var scope = telemetry.Begin(TelemetryOperation.Api)) scope.Complete(TelemetryOutcome.Success);
        Assert.Single(collector.Records); Assert.Null(Activity.Current);
    }

    [Fact]
    public void Start_listener_failure_restores_context_and_does_not_break_processing()
    {
        using var listener = Listen(); listener.ActivityStarted = _ => throw new InvalidOperationException("SECRET");
        ActivitySource.AddActivityListener(listener);
        var collector = new Collector(); var telemetry = new SafeTelemetry(collector);
        using var outside = new Activity("outside").Start();
        using (var scope = telemetry.Begin(TelemetryOperation.Api)) scope.Complete(TelemetryOutcome.Success);
        Assert.Single(collector.Records); Assert.Same(outside, Activity.Current);
    }

    [Theory]
    [InlineData("None", typeof(NoOpTelemetryAdapter))]
    [InlineData("Local", typeof(LocalTelemetryAdapter))]
    public async Task Configuration_selects_one_adapter_and_public_contract_stays_available(string provider, Type type)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["Telemetry:Provider"] = provider })));
        using var client = factory.CreateClient();
        Assert.IsType(type, factory.Services.GetRequiredService<ITelemetryAdapter>());
        Assert.Single(factory.Services.GetServices<ITelemetryAdapter>());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }

    [Fact]
    public async Task Api_outage_isolation_and_server_correlation_cover_success_and_safe_errors()
    {
        var collector = new Collector();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.Replace(ServiceDescriptor.Singleton<ITelemetryAdapter>(collector))));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "SECRET-token");
        client.DefaultRequestHeaders.Add("traceparent", "00-11111111111111111111111111111111-2222222222222222-01");
        var response = await client.GetAsync("/api/v1/vehicles?contact=SECRET");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var record = Assert.Single(collector.Records);
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), record.Context.CorrelationId);
        Assert.Equal(TelemetryOutcome.Rejected, record.Outcome); Assert.Null(record.Context.ParentSpanId);
        Assert.NotEqual("11111111111111111111111111111111", record.Context.TraceId);
        Assert.DoesNotContain("SECRET", JsonSerializer.Serialize(record));
        await using var broken = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.Replace(ServiceDescriptor.Singleton<ITelemetryAdapter>(new Broken()))));
        using var brokenClient = broken.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await brokenClient.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await brokenClient.GetAsync("/api/v1/vehicles")).StatusCode);
    }
}
