using System.Net;
using System.Text.Json;
using FaultCode.Application.Incidents;
using FaultCode.Infrastructure;
using static FaultCode.IntegrationTests.EngineeringTests;
using static FaultCode.IntegrationTests.EntraAuthenticationTests;

namespace FaultCode.IntegrationTests;

public sealed class IncidentTrackingTests
{
    private static readonly Guid ExternalId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static async Task<(TestEngineeringStore Store, Guid Id)> Fixture()
    {
        var store = new TestEngineeringStore(); var id = Guid.NewGuid();
        await store.NotificationExecute(Tenant, tx =>
        {
            tx.Context.Incidents.Incidents.Add(new(id, Tenant, Client, "event", "SYN-EPS:UNLISTED", "EPS", "SYN-STEERING-DEMO", "demo-1",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Guid.NewGuid().ToString("N"), new(Guid.NewGuid(), "Silverado", 1, Guid.NewGuid(), "Driver", 1, Actor,
                new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, null)), null, "Received", 1, [], [])); return 0;
        }); return (store, id);
    }
    [Fact]
    public async Task Accepted_identity_is_persisted_and_resolution_updates_the_same_provider_record()
    {
        var (store, id) = await Fixture(); var calls = new List<HttpMethod>();
        using var http = new HttpClient(new Handler(async request =>
        {
            calls.Add(request.Method);
            Assert.Equal("api.datadoghq.com", request.RequestUri!.Host);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-private-token", request.Headers.Authorization.Parameter);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync()); var data = body.RootElement.GetProperty("data");
            var a = data.GetProperty("attributes");
            if (request.Method == HttpMethod.Post)
            {
                Assert.True(a.GetProperty("is_test").GetBoolean());
                Assert.Equal(0, a.GetProperty("notification_handles").GetArrayLength());
                Assert.Contains(id.ToString(), a.GetProperty("title").GetString());
            }
            if (request.Method == HttpMethod.Patch)
            {
                Assert.Equal(ExternalId.ToString(), data.GetProperty("id").GetString());
                Assert.Equal("resolved", a.GetProperty("fields").GetProperty("state").GetProperty("value").GetString());
            }
            return Receipt(id);
        }));
        var service = new IncidentTrackingService(store, new DatadogIncidentTracker(http, "test-private-token"), TimeProvider.System);
        await service.Sync(Tenant, id, default); await service.Sync(Tenant, id, default);
        Assert.Single(calls);
        var i = store.NotificationSnapshot().Context.Incidents.Incidents.Single();
        Assert.Equal(1, i.Version); Assert.Equal("Linked", i.Tracking!.State); Assert.Equal(ExternalId.ToString(), i.Tracking.Id);
        Assert.Equal("https://app.datadoghq.com/incidents/42", i.Tracking.Url);
        await store.NotificationExecute(Tenant, tx => { var old = tx.Context.Incidents.Incidents.Single(); tx.Context.Incidents.Incidents[0] = old with { Status = "Resolved", Version = 2, Resolution = new("Recorded findings", Actor, Client, DateTimeOffset.UtcNow, 2) }; return 0; });
        await service.Sync(Tenant, id, default); await service.Sync(Tenant, id, default);
        Assert.Equal(new[] { HttpMethod.Post, HttpMethod.Patch }, calls);
        Assert.Equal(2, store.NotificationSnapshot().Context.Incidents.Incidents.Single().Tracking!.Version);
    }
    [Theory]
    [InlineData("timeout", "Unknown", 1)]
    [InlineData("invalid-receipt", "Unknown", 1)]
    [InlineData("denied", "Failed", 2)]
    [InlineData("seat", "Failed", 2)]
    public async Task Failure_never_fabricates_a_link_or_blindly_recreates_uncertain_incidents(string outcome, string state, int expectedCalls)
    {
        var (store, id) = await Fixture(); var calls = 0;
        using var http = new HttpClient(new Handler(request =>
        {
            calls++;
            if (outcome == "timeout") throw new HttpRequestException("Do not expose raw request details");
            return Task.FromResult(outcome is "denied" or "seat" ? new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = JsonContent(new { errors = new[] { new { title = outcome == "seat" ? "Seat Required" : "Forbidden" } } })
            } : Receipt(Guid.NewGuid()));
        }));
        var service = new IncidentTrackingService(store, new DatadogIncidentTracker(http, "test-private-token"), TimeProvider.System);
        await service.Sync(Tenant, id, default); await service.Sync(Tenant, id, default);
        var record = store.NotificationSnapshot().Context.Incidents.Incidents.Single().Tracking!;
        Assert.Equal(expectedCalls, calls); Assert.Equal(state, record.State); Assert.Null(record.Url); Assert.Null(record.Id);
        Assert.DoesNotContain("raw", record.FailureCode!);
        if (outcome == "seat") Assert.Equal("datadog-seat-required", record.FailureCode);
    }
    [Fact]
    public async Task Concurrent_sync_uses_one_claim_and_missing_credentials_make_no_external_request()
    {
        var (store, id) = await Fixture(); var calls = 0; var entered = new TaskCompletionSource(); var release = new TaskCompletionSource();
        using var http = new HttpClient(new Handler(async request => { calls++; entered.SetResult(); await release.Task; return Receipt(id); }));
        var service = new IncidentTrackingService(store, new DatadogIncidentTracker(http, "test-private-token"), TimeProvider.System);
        var first = service.Sync(Tenant, id, default); await entered.Task; await service.Sync(Tenant, id, default); release.SetResult(); await first; Assert.Equal(1, calls);
        var (other, otherId) = await Fixture(); var missing = new IncidentTrackingService(other, new DatadogIncidentTracker(http, ""), TimeProvider.System);
        await missing.Sync(Tenant, otherId, default); Assert.Equal(1, calls);
        Assert.Equal("datadog-not-configured", other.NotificationSnapshot().Context.Incidents.Incidents.Single().Tracking!.FailureCode);
    }
    private static HttpResponseMessage Receipt(Guid id) => new(HttpStatusCode.OK) { Content = JsonContent(new { data = new { type = "incidents", id = ExternalId, attributes = new { public_id = 42, title = $"Fault Code {id:D}: SYN-EPS:UNLISTED" } } }) };
    private static StringContent JsonContent(object body) => new(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request); }
}
