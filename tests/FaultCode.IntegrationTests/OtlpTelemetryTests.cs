using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using FaultCode.Api.Configuration;
using FaultCode.Application.Telemetry;
using FaultCode.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FaultCode.IntegrationTests;

public sealed class OtlpTelemetryTests
{
    // Actual HTTP receiver: no fake exporter and no conditional/no-op acceptance guard.
    internal sealed class Receiver(WebApplication app) : IAsyncDisposable
    {
        public readonly ConcurrentQueue<(string Path, byte[] Body, string? Type)> Messages = [];
        public bool Fail, Hold;
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Endpoint => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        public static async Task<Receiver> Start()
        {
            var builder = WebApplication.CreateSlimBuilder(); builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
            var app = builder.Build(); var receiver = new Receiver(app);
            app.MapPost("/v1/{signal}", async context =>
            {
                using var buffer = new MemoryStream(); await context.Request.Body.CopyToAsync(buffer);
                receiver.Messages.Enqueue((context.Request.Path.Value!, buffer.ToArray(), context.Request.ContentType));
                receiver.Entered.TrySetResult();
                if (receiver.Hold) await receiver.Release.Task.WaitAsync(TimeSpan.FromSeconds(15));
                context.Response.StatusCode = receiver.Fail ? 503 : 200;
                context.Response.ContentType = "application/x-protobuf";
            });
            await app.StartAsync(); return receiver;
        }
        public async ValueTask DisposeAsync() { Release.TrySetResult(); await app.DisposeAsync(); }
        public Proto[] Items(string signal) => Messages.Where(x => x.Path == "/v1/" + signal)
            .SelectMany(x => Proto.Parse(x.Body).Children(1)).SelectMany(x => x.Children(2)).SelectMany(x => x.Children(2)).ToArray();
    }
    // Minimal wire decoder for the official OTLP schema; asserts fields, IDs and attributes rather than byte presence alone.
    internal sealed record Field(int Number, int Wire, byte[] Data);
    internal sealed record Proto(Field[] Fields)
    {
        public static Proto Parse(byte[] bytes)
        {
            var fields = new List<Field>(); var at = 0;
            ulong Varint() { ulong n = 0; var shift = 0; byte b; do { b = bytes[at++]; n |= (ulong)(b & 127) << shift; shift += 7; } while (b >= 128); return n; }
            while (at < bytes.Length)
            {
                var tag = Varint(); var wire = (int)(tag & 7); var number = (int)(tag >> 3);
                if (wire == 0) { fields.Add(new(number, wire, BitConverter.GetBytes(Varint()))); continue; }
                var size = wire switch { 1 => 8, 2 => checked((int)Varint()), 5 => 4, _ => throw new InvalidDataException() };
                fields.Add(new(number, wire, bytes[at..(at + size)])); at += size;
            }
            return new(fields.ToArray());
        }
        public IEnumerable<Proto> Children(int field) => Fields.Where(x => x.Number == field && x.Wire == 2).Select(x => Parse(x.Data));
        public byte[] Bytes(int field) => Fields.FirstOrDefault(x => x.Number == field)?.Data ?? [];
        public string Text(int field) => Encoding.UTF8.GetString(Bytes(field));
        public string Hex(int field) => Convert.ToHexStringLower(Bytes(field));
        public Dictionary<string, Proto> Attributes(int field) => Children(field).ToDictionary(x => x.Text(1), x => x.Children(2).Single());
    }

    [Fact]
    public async Task Real_OTLP_delivers_nested_traces_correlated_logs_and_bounded_metrics()
    {
        await using var receiver = await Receiver.Start();
        using var adapter = new OtlpTelemetryAdapter(new(receiver.Endpoint, 1, "synthetic-model-2026-10-21"));
        var telemetry = new SafeTelemetry(adapter); var correlation = Guid.NewGuid().ToString("N");
        using var outside = new Activity("SECRET-outside").AddBaggage("contact", "SECRET-contact").Start();
        using (var api = telemetry.Begin(TelemetryOperation.Api, correlation))
        {
            using (var agent = telemetry.Begin(TelemetryOperation.Agent))
            {
                using (var mcp = telemetry.Begin(TelemetryOperation.Mcp))
                {
                    using (var retrieval = telemetry.Begin(TelemetryOperation.Retrieval)) retrieval.Complete(TelemetryOutcome.Success);
                    mcp.Complete(TelemetryOutcome.Success);
                }
                using (var model = telemetry.Begin(TelemetryOperation.Model)) { model.Report(TelemetryError.Dependency); model.Complete(TelemetryOutcome.Failed, 12); }
                agent.Complete(TelemetryOutcome.Success);
            }
            api.Complete(TelemetryOutcome.Success);
        }
        Assert.Same(outside, Activity.Current); Assert.True(adapter.ForceFlush());
        var spans = receiver.Items("traces"); Assert.Equal(5, spans.Length);
        var root = Assert.Single(spans, x => x.Text(5) == "Api"); Assert.Empty(root.Bytes(4));
        var agentSpan = Assert.Single(spans, x => x.Text(5) == "Agent"); Assert.Equal(root.Hex(2), agentSpan.Hex(4));
        var mcpSpan = Assert.Single(spans, x => x.Text(5) == "Mcp"); Assert.Equal(agentSpan.Hex(2), mcpSpan.Hex(4));
        Assert.Equal(mcpSpan.Hex(2), Assert.Single(spans, x => x.Text(5) == "Retrieval").Hex(4));
        var logs = receiver.Items("logs"); Assert.Equal(5, logs.Length);
        foreach (var span in spans)
        {
            Assert.Equal(root.Hex(1), span.Hex(1));
            var attributes = span.Attributes(9); Assert.Equal(correlation, attributes["faultcode.correlation_id"].Text(1));
            var log = Assert.Single(logs, x => x.Hex(10) == span.Hex(2));
            Assert.Equal(span.Hex(1), log.Hex(9)); Assert.Equal(correlation, log.Attributes(6)["CorrelationId"].Text(1));
            Assert.False(attributes.ContainsKey("gen_ai.usage.output_tokens"));
        }
        var modelSpan = Assert.Single(spans, x => x.Text(5) == "Model");
        Assert.Equal("synthetic-model-2026-10-21", modelSpan.Attributes(9)["gen_ai.request.model"].Text(1));
        Assert.Equal("Dependency", modelSpan.Attributes(9)["error.type"].Text(1));
        var metrics = receiver.Items("metrics"); Assert.Contains(metrics, x => x.Text(1) == "faultcode.operation.duration");
        Assert.Contains(metrics, x => x.Text(1) == "faultcode.model.input_tokens");
        Assert.DoesNotContain(metrics, x => x.Text(1) == "faultcode.model.output_tokens");
        foreach (var metric in metrics)
        {
            var data = metric.Children(5).Concat(metric.Children(7)).Concat(metric.Children(9));
            foreach (var point in data.SelectMany(x => x.Children(1)))
            {
                var tagField = metric.Fields.Any(x => x.Number == 9) ? 9 : 7;
                Assert.All(point.Attributes(tagField).Keys, key => Assert.Contains(key, new[] { "operation", "outcome" }));
            }
        }
        Assert.All(receiver.Messages, x => { Assert.Equal("application/x-protobuf", x.Type); Assert.DoesNotContain("SECRET", Encoding.UTF8.GetString(x.Body)); });
    }

    [Fact]
    public async Task Zero_trace_sampling_applies_to_all_children_but_preserves_logs_metrics_and_missing_usage()
    {
        await using var receiver = await Receiver.Start(); using var adapter = new OtlpTelemetryAdapter(new(receiver.Endpoint, 0));
        var telemetry = new SafeTelemetry(adapter);
        using (var root = telemetry.Begin(TelemetryOperation.Api))
        { using (var child = telemetry.Begin(TelemetryOperation.Model)) child.Complete(TelemetryOutcome.Success); root.Complete(TelemetryOutcome.Success); }
        Assert.True(adapter.ForceFlush()); Assert.Empty(receiver.Items("traces"));
        Assert.Equal(2, receiver.Items("logs").Length); Assert.NotEmpty(receiver.Items("metrics"));
        Assert.DoesNotContain(receiver.Messages, x => Encoding.UTF8.GetString(x.Body).Contains("gen_ai.usage", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Otlp")]
    [InlineData("Datadog")]
    public async Task Api_selects_one_exporter_and_exports_no_request_identity_or_framework_logs(string provider)
    {
        await using var receiver = await Receiver.Start();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["Telemetry:Provider"] = provider, ["Telemetry:Endpoint"] = receiver.Endpoint, ["Telemetry:TraceSampleRatio"] = "1" })));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("traceparent", "00-11111111111111111111111111111111-2222222222222222-01");
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "SECRET-header");
        var response = await client.GetAsync("/api/v1/vehicles?contact=SECRET-query"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var adapter = Assert.IsType<OtlpTelemetryAdapter>(Assert.Single(factory.Services.GetServices<ITelemetryAdapter>())); Assert.True(adapter.ForceFlush());
        var span = Assert.Single(receiver.Items("traces")); Assert.Equal("Api", span.Text(5)); Assert.NotEqual("11111111111111111111111111111111", span.Hex(1));
        Assert.Empty(span.Bytes(4)); Assert.Single(receiver.Items("logs"));
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), span.Attributes(9)["faultcode.correlation_id"].Text(1));
        Assert.All(receiver.Messages, x => Assert.DoesNotContain("SECRET", Encoding.UTF8.GetString(x.Body)));
    }

    private sealed class RetryHttp : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(++Calls == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
        }
    }
    [Fact]
    public async Task Sanitized_HTTP_attempts_report_retry_failure_success_and_cancellation_without_URL_or_headers()
    {
        await using var receiver = await Receiver.Start(); using var adapter = new OtlpTelemetryAdapter(new(receiver.Endpoint, 1));
        var telemetry = new SafeTelemetry(adapter); var upstream = new RetryHttp();
        using var client = new HttpClient(new SanitizedHttpTelemetryHandler(telemetry) { InnerHandler = upstream });
        using (var model = telemetry.Begin(TelemetryOperation.Model))
        {
            using var first = await client.GetAsync("https://example.test/SECRET-path?token=SECRET-query"); Assert.Equal(HttpStatusCode.ServiceUnavailable, first.StatusCode);
            using var second = await client.GetAsync("https://example.test/SECRET-path"); Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("https://example.test/SECRET-path", cancel.Token));
            model.Complete(TelemetryOutcome.Success);
        }
        Assert.Equal(2, upstream.Calls); Assert.True(adapter.ForceFlush());
        var spans = receiver.Items("traces"); var modelSpan = Assert.Single(spans, x => x.Text(5) == "Model");
        var attempts = spans.Where(x => x.Text(5) == "Http").ToArray(); Assert.Equal(3, attempts.Length);
        Assert.All(attempts, x => Assert.Equal(modelSpan.Hex(2), x.Hex(4)));
        Assert.Equal(new[] { "Cancelled", "Failed", "Success" }, attempts.Select(x => x.Attributes(9)["faultcode.outcome"].Text(1)).Order());
        Assert.All(receiver.Messages, x => Assert.DoesNotContain("SECRET", Encoding.UTF8.GetString(x.Body)));
    }

    [Theory]
    [InlineData("http://example.com", "Otlp", 1)]
    [InlineData("https://example.com?token=SECRET", "Otlp", 1)]
    [InlineData("https://user:SECRET@example.com", "Otlp", 1)]
    [InlineData("https://example.com", "Datadog", 1)]
    [InlineData("http://127.0.0.1:4318", "Otlp", -1)]
    [InlineData("http://127.0.0.1:4318", "Otlp", double.NaN)]
    [InlineData("http://127.0.0.1:4318", "Otlp", 2)]
    public void Unsafe_export_configuration_is_invalid(string endpoint, string provider, double ratio) =>
        Assert.False(new TelemetryOptions { Provider = provider, Endpoint = endpoint, TraceSampleRatio = ratio }.Valid);

    [Fact]
    public void Environment_cannot_silently_activate_retries_headers_or_other_destinations()
    {
        const string key = "OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY"; var prior = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, "disk");
            Assert.False(new TelemetryOptions { Provider = "Otlp", Endpoint = "http://127.0.0.1:4318" }.Valid);
            var error = Assert.Throws<ArgumentException>(() => new OtlpTelemetryAdapter(new("http://127.0.0.1:4318")));
            Assert.DoesNotContain("disk", error.Message); Assert.True(new TelemetryOptions().Valid);
        }
        finally { Environment.SetEnvironmentVariable(key, prior); }
    }

    [Fact]
    public async Task Shutdown_during_unresponsive_receiver_is_bounded_and_idempotent()
    {
        await using var receiver = await Receiver.Start(); receiver.Hold = true;
        using var adapter = new OtlpTelemetryAdapter(new(receiver.Endpoint, 1)); var telemetry = new SafeTelemetry(adapter);
        using (var work = telemetry.Begin(TelemetryOperation.Api)) work.Complete(TelemetryOutcome.Success);
        await receiver.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var elapsed = Stopwatch.StartNew(); adapter.Dispose(); adapter.Dispose();
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(10));
        Assert.Equal(42, await telemetry.Run(TelemetryOperation.Api, "", () => Task.FromResult(42)));
        receiver.Release.TrySetResult();
    }

    [Fact]
    public async Task Saturated_outage_drops_bounded_telemetry_without_blocking_business_work()
    {
        await using var receiver = await Receiver.Start(); receiver.Hold = true; receiver.Fail = true;
        using var adapter = new OtlpTelemetryAdapter(new(receiver.Endpoint, 1)); var telemetry = new SafeTelemetry(adapter);
        using (var first = telemetry.Begin(TelemetryOperation.Api)) first.Complete(TelemetryOutcome.Success);
        await receiver.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var elapsed = Stopwatch.StartNew();
        for (var i = 0; i < 5000; i++)
            Assert.Equal(42, await telemetry.Run(TelemetryOperation.Investigation, "", () => Task.FromResult(42)));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(3));
        var original = new InvalidOperationException("SECRET-error");
        Assert.Same(original, await Assert.ThrowsAsync<InvalidOperationException>(() => telemetry.Run<int>(TelemetryOperation.Model, "", () => throw original)));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => telemetry.Run<int>(TelemetryOperation.Model, "", () => Task.FromCanceled<int>(cancel.Token)));
        receiver.Release.TrySetResult(); adapter.ForceFlush();
        Assert.InRange(receiver.Items("traces").Length, 1, 4999); Assert.InRange(receiver.Items("logs").Length, 1, 4999);
        Assert.All(receiver.Messages, x => Assert.DoesNotContain("SECRET", Encoding.UTF8.GetString(x.Body)));
        Assert.Equal(43, await telemetry.Run(TelemetryOperation.Api, "", () => Task.FromResult(43)));
    }
}
