using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FaultCode.Api.Contracts;
using FaultCode.Application.Fleet;
using FaultCode.Application.Incidents;
using FaultCode.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static FaultCode.IntegrationTests.EntraAuthenticationTests;

namespace FaultCode.IntegrationTests;

// Test-only one-process transactional fixture. It cannot establish production durability.
internal sealed class TestIncidentStore(IncidentContext context) : IIncidentStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, IncidentState> tenants = [];
    private IncidentContext context = context;
    public bool FailCommit { get; set; }
    public bool ContextUnavailable { get; set; }
    public int ContextReads { get; private set; }
    public void SetContext(IncidentContext value) { lock (gate) context = value; }
    public Task<IncidentState> ReadAsync(string tenantId, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); lock (gate) return Task.FromResult(tenants.TryGetValue(tenantId, out var s) ? s.Copy() : new IncidentState()); }
    public Task<T> TransactAsync<T>(string tenantId, Func<IncidentTransaction, T> operation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); lock (gate)
        {
            var working = tenants.TryGetValue(tenantId, out var s) ? s.Copy() : new IncidentState();
            var result = operation(new(working, () =>
            {
                ContextReads++; if (ContextUnavailable) throw new IncidentException(503);
                return new(context.Vehicles.Where(v => v.TenantId == tenantId).ToArray(), context.Drivers.Where(d => d.TenantId == tenantId).ToArray(),
                    context.Associations.ToArray(), context.Faults.Where(f => f.TenantId == tenantId).ToArray());
            }));
            ct.ThrowIfCancellationRequested(); if (FailCommit) throw new IncidentException(503);
            tenants[tenantId] = working.Copy(); return Task.FromResult(result);
        }
    }
}
internal sealed class IncidentClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}
public sealed class IncidentTests
{
    private static readonly Guid VehicleId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid DriverId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly FleetCaller Caller = new(Tenant, Actor, Client, "demo-correlation", true, false, true, false);
    private static IncidentInput Input(string source = "SYN-EVENT-1", string code = "SYN-EPS:001") => new(VehicleId, DriverId, source, code, "EPS", "SYN-MODEL-1", "1.0", Now.AddMinutes(-1));
    private static IncidentContext Context()
    {
        var content = new FaultContent("Approved demo fault", "demo-EPS", 1, ["Reduced demo assist"], [new("SYN-DOC-1", "1")],
            [new("approved", "dealership", "Demo inspection", true, "SYN-MODEL-1", "1.0", new("SYN-PATCH-1", "1.0")),
             new("unapproved", "dealership", "Unapproved", false, "SYN-MODEL-1", "1.0", new("SYN-PATCH-2", "1.0")),
             new("other-model", "guidance", "Other model", true, "SYN-MODEL-2", "1.0", null)]);
        return new([new(VehicleId, Tenant, "SYN-VEHICLE-1", "EPS", true, 3)],
            [new(DriverId, Tenant, "DRV-1", Actor, "Demo Driver", null, false, true, 1)],
            [new(Guid.NewGuid(), VehicleId, DriverId, Now.AddHours(-2), null)],
            [new(Guid.NewGuid(), Tenant, "SYN-EPS:001", "EPS", false, 3, content with { Description = "Private draft" },
                [new(1, content, "draft", Actor, Now.AddHours(-1))], [new(2, content, Actor, Now.AddHours(-1))])]);
    }
    private static CreateIncidentRequest Body(IncidentInput i) => new(i.VehicleId, i.DriverId, i.SourceEventId, i.Code, i.System, i.Model, i.SoftwareVersion, i.OccurredAt);
    private static void Auth(HttpClient client, Discovery keys, string scope = "faults.write knowledge.read faults.read vehicles.read drivers.read", string role = "Operator", bool machine = false, string actor = Actor) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthorizationTests.Token(keys.Key, machine ? null : scope, machine ? scope.Split(' ') : [role], machine, actor));
    private static HttpRequestMessage Request(HttpMethod method, string path, object? body = null, string? version = null, string? key = null)
    {
        var request = new HttpRequestMessage(method, path); if (body is not null) request.Content = JsonContent.Create(body);
        if (version is not null) request.Headers.TryAddWithoutValidation("If-Match", version);
        if (key is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", key); return request;
    }
    private static async Task<JsonElement> Send(HttpClient client, HttpRequestMessage request, int status = 200)
    {
        using (request) using (var response = await client.SendAsync(request))
        {
            var raw = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == (HttpStatusCode)status, $"Expected {status}, got {response.StatusCode}: {raw}");
            using var json = JsonDocument.Parse(raw);
            Assert.NotEmpty(response.Headers.GetValues("X-Correlation-ID"));
            if (status >= 400)
            { Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType); Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), json.RootElement.GetProperty("correlationId").GetString()); }
            else if (json.RootElement.TryGetProperty("version", out var v)) Assert.Equal($"\"{v.GetInt64()}\"", response.Headers.ETag?.Tag);
            if (status == 201) Assert.NotNull(response.Headers.Location);
            return json.RootElement.Clone();
        }
    }
    [Fact]
    public async Task Unmatched_queue_filters_before_count_and_paging_and_retains_owner_boundary()
    {
        var store = new TestIncidentStore(Context()); var service = new IncidentService(store, new IncidentClock(Now));
        await service.Create(Caller, Input("known"), "known", default);
        var unknown = await service.Create(Caller, Input("unknown", "SYN-EPS:UNKNOWN"), "unknown", default);
        var page = await service.List(Caller, 1, 1, null, null, default, unmatchedOnly: true);
        Assert.Equal(1, page.TotalCount); Assert.Equal(unknown.Id, Assert.Single(page.Items).Id);
        Assert.Null(page.Items[0].Knowledge);
        Assert.Empty((await service.List(Caller, 2, 1, null, null, default, true)).Items);
        Assert.Equal(2, (await service.List(Caller, 1, 20, null, null, default)).TotalCount);
        var otherOwner = Caller with { ActorId = Client, AllVehicles = false, OwnVehicles = true };
        Assert.Equal(0, (await service.List(otherOwner, 1, 20, null, null, default, true)).TotalCount);
        Assert.Equal(0, (await service.List(Caller with { TenantId = Client }, 1, 20, null, null, default, true)).TotalCount);
    }
    [Fact]
    public async Task Concurrent_source_retries_actor_key_changes_and_dependency_outage_replay_are_atomic()
    {
        var store = new TestIncidentStore(Context()); var clock = new IncidentClock(Now); var service = new IncidentService(store, clock);
        var input = Input();
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(n => Task.Run(() => service.Create(Caller, input, "key-" + n, default))));
        Assert.Single(results.Distinct()); var state = await store.ReadAsync(Tenant, default); Assert.Single(state.Incidents); Assert.Single(state.Audit); Assert.Single(state.Sources); Assert.Single(state.Incidents[0].Work);
        store.ContextUnavailable = true; clock.Now = Now.AddDays(20);
        var replay = await service.Create(Caller with { ActorId = Client }, input, "different-actor", default); Assert.Equal(results[0], replay); Assert.Equal(1, store.ContextReads);
        Assert.Equal(409, (await Assert.ThrowsAsync<IncidentException>(() => service.Create(Caller, input with { Code = "SYN-EPS:002" }, "another-key", default))).Status);
        clock.Now = Now;
        Assert.Equal(503, (await Assert.ThrowsAsync<IncidentException>(() => service.Create(Caller, Input("new"), "new", default))).Status);
        Assert.Single((await store.ReadAsync(Tenant, default)).Incidents);
        store.ContextUnavailable = false;
        var separateSource = await service.Create(Caller with { ClientId = Actor }, input, "same-event-other-client", default); Assert.NotEqual(replay.Id, separateSource.Id);
        var updates = await Task.WhenAll(Enumerable.Range(0, 10).Select(n => Task.Run(async () =>
        { try { await service.Transition(Caller, replay.Id, "Investigating", 1, "update-" + n, default); return 200; } catch (IncidentException e) { return e.Status; } })));
        Assert.Equal(1, updates.Count(x => x == 200)); Assert.Equal(9, updates.Count(x => x == 409));
    }
    [Fact]
    public async Task Fail_commit_callback_and_cancellation_roll_back_incident_source_history_work_audit_and_replay()
    {
        var store = new TestIncidentStore(Context()) { FailCommit = true }; var service = new IncidentService(store, new IncidentClock(Now));
        await Assert.ThrowsAsync<IncidentException>(() => service.Create(Caller, Input(), "create", default));
        var empty = await store.ReadAsync(Tenant, default); Assert.Empty(empty.Incidents); Assert.Empty(empty.Replays); Assert.Empty(empty.Sources); Assert.Empty(empty.Audit);
        store.FailCommit = false; var result = await service.Create(Caller, Input(), "create", default); store.FailCommit = true;
        await Assert.ThrowsAsync<IncidentException>(() => service.Fail(Caller, result.Id, "processing-unavailable", 1, "failure", default));
        var unchanged = await store.ReadAsync(Tenant, default); Assert.Single(unchanged.Audit); Assert.Single(unchanged.Incidents[0].History); Assert.Equal("Pending", unchanged.Incidents[0].Work[0].State);
        store.FailCommit = false;
        await Assert.ThrowsAsync<IncidentException>(() => store.TransactAsync<int>(Tenant, tx => { tx.State.Incidents.Clear(); throw new IncidentException(409); }, default));
        using var cancel = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.TransactAsync(Tenant, tx => { tx.State.Incidents.Clear(); cancel.Cancel(); return 0; }, cancel.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.Create(Caller, Input("cancel"), "cancel", cancel.Token));
        unchanged.Incidents.Clear(); Assert.Single((await store.ReadAsync(Tenant, default)).Incidents);
        await service.Fail(Caller, result.Id, "processing-unavailable", 1, "failure", default);
        var failed = await service.Get(Caller, result.Id, default); Assert.Equal("Failed", failed.Work[0].State); Assert.Equal(2, failed.Version);
    }
    [Fact]
    public async Task Historical_context_and_access_survive_reassignment_deactivation_and_knowledge_changes()
    {
        var context = Context(); var store = new TestIncidentStore(context); var service = new IncidentService(store, new IncidentClock(Now));
        var known = await service.Create(Caller, Input(), "known", default);
        var unknown = await service.Create(Caller, Input("unknown", "SYN-EPS:UNKNOWN"), "unknown", default);
        Assert.Null((await service.Get(Caller, unknown.Id, default)).Knowledge);
        store.SetContext(new([context.Vehicles[0] with { Active = false, Identifier = "SYN-CHANGED", Version = 4 }],
            [context.Drivers[0] with { Active = false, OwnerObjectId = Client, Version = 2 }],
            [context.Associations[0] with { Until = Now }], [context.Faults[0] with { Retired = true, Publications = [] }]));
        var originalOwner = Caller with { AllVehicles = false, OwnVehicles = true };
        var snapshot = await service.Get(originalOwner, known.Id, default);
        Assert.Equal("SYN-VEHICLE-1", snapshot.Fleet.VehicleIdentifier); Assert.Equal(3, snapshot.Fleet.VehicleVersion); Assert.Null(snapshot.Fleet.Association.Until);
        Assert.Equal(2, snapshot.Knowledge!.PublicationVersion); Assert.Equal(Actor, snapshot.Knowledge.ApprovedBy); Assert.Single(snapshot.Knowledge.Content.Remedies);
        Assert.Equal(404, (await Assert.ThrowsAsync<IncidentException>(() => service.Get(originalOwner with { ActorId = Client }, known.Id, default))).Status);
        Assert.Empty((await service.List(originalOwner with { ActorId = Client }, 1, 1, null, null, default)).Items);
        Assert.Equal(0, (await service.List(originalOwner with { ActorId = Client }, 1, 1, null, null, default)).TotalCount);
        Assert.Equal(2, (await service.List(originalOwner, int.MaxValue, 100, null, null, default)).TotalCount);
        Assert.Empty((await service.List(originalOwner, int.MaxValue, 100, null, null, default)).Items);
        Assert.Equal(404, (await Assert.ThrowsAsync<IncidentException>(() => service.Get(Caller with { TenantId = Client }, known.Id, default))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<IncidentException>(() => service.Create(Caller, Input("inactive"), "inactive", default))).Status);
        Assert.Equal(known, await service.Create(Caller, Input(), "replay-after-change", default));
    }
    [Fact]
    public async Task Invalid_context_data_and_unpublished_retired_inapplicable_knowledge_are_explicit()
    {
        var context = Context(); var store = new TestIncidentStore(context); var service = new IncidentService(store, new IncidentClock(Now));
        foreach (var invalid in new[] { Input() with { VehicleId = Guid.Empty }, Input() with { DriverId = Guid.Empty }, Input() with { SourceEventId = "bad/source" }, Input() with { Model = "real-model" },
            Input() with { SoftwareVersion = "*" }, Input() with { Code = "P1234" }, Input() with { System = "brakes" }, Input() with { OccurredAt = Now.AddSeconds(1) },
            Input() with { OccurredAt = Now.AddDays(-8) }, Input() with { OccurredAt = Now.AddHours(-3) }, Input() with { OccurredAt = Now.ToOffset(TimeSpan.FromHours(-4)) } })
            Assert.Equal(400, (await Assert.ThrowsAsync<IncidentException>(() => service.Create(Caller, invalid, "invalid", default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<IncidentException>(() => service.Create(Caller, Input() with { VehicleId = Guid.NewGuid() }, "missing", default))).Status);
        foreach (var bad in new[] {
            context with { Vehicles = [context.Vehicles[0] with { Active = false }] },
            context with { Drivers = [context.Drivers[0] with { Active = false }] },
            context with { Drivers = [context.Drivers[0] with { OwnerObjectId = "invalid" }] },
            context with { Associations = [context.Associations[0] with { Until = Now }] },
            context with { Associations = [context.Associations[0] with { From = Now }] },
            context with { Vehicles = [context.Vehicles[0] with { System = "steering-ADAS" }] } })
        { store.SetContext(bad); Assert.Equal(400, (await Assert.ThrowsAsync<IncidentException>(() => service.Create(Caller, Input(), "bad-association", default))).Status); }
        store.SetContext(context with { Drivers = [context.Drivers[0] with { TenantId = Client }] });
        Assert.Equal(404, (await Assert.ThrowsAsync<IncidentException>(() => service.Create(Caller, Input(), "cross-tenant", default))).Status);
        foreach (var f in new[] { context.Faults[0] with { Publications = [] }, context.Faults[0] with { Retired = true } })
        {
            store.SetContext(context with { Faults = [f] }); var result = await service.Create(Caller, Input(Guid.NewGuid().ToString("N")), Guid.NewGuid().ToString("N"), default);
            Assert.Null((await service.Get(Caller, result.Id, default)).Knowledge);
        }
        store.SetContext(context); var none = await service.Create(Caller, Input("inapplicable") with { SoftwareVersion = "2.0" }, "inapplicable", default);
        Assert.Empty((await service.Get(Caller, none.Id, default)).Knowledge!.Content.Remedies);
    }
    [Fact]
    public async Task All_transition_edges_are_enforced_by_application_with_no_corruption()
    {
        var states = new[] { "Received", "Investigating", "Escalated", "Resolved", "NotificationPending", "Notified" };
        foreach (var from in states) foreach (var to in states)
        {
            var store = new TestIncidentStore(Context()); var service = new IncidentService(store, new IncidentClock(Now));
            var result = await service.Create(Caller, Input(), "create", default);
            var route = from switch
            {
                "Received" => Array.Empty<string>(),
                "Investigating" => ["Investigating"],
                "Escalated" => ["Investigating", "Escalated"],
                "Resolved" => ["Investigating", "Resolved"],
                "NotificationPending" => ["Investigating", "Resolved", "NotificationPending"],
                _ => new[] { "Investigating", "Resolved", "NotificationPending", "Notified" }
            };
            foreach (var status in route) result = await service.Transition(Caller, result.Id, status, result.Version, "stage-" + result.Version, default);
            var before = await service.Get(Caller, result.Id, default);
            if (IncidentRules.CanTransition(from, to))
            { var next = await service.Transition(Caller, result.Id, to, result.Version, "test", default); Assert.Equal(result.Version + 1, next.Version); }
            else
            { Assert.Equal(409, (await Assert.ThrowsAsync<IncidentException>(() => service.Transition(Caller, result.Id, to, result.Version, "test", default))).Status); Assert.Equal(before, await service.Get(Caller, result.Id, default)); }
        }
    }
    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    [InlineData("W/\"1\"")]
    [InlineData("\"01\"")]
    [InlineData("*")]
    [InlineData("\"0\"")]
    [InlineData("\"1\",\"2\"")]
    public async Task Mutation_preconditions_are_strong(string? version)
    {
        using var keys = new Discovery(); await using var factory = CreateFactory(keys); using var client = factory.CreateClient(); Auth(client, keys);
        await Send(client, Request(HttpMethod.Put, "/api/v1/incidents/" + Guid.NewGuid() + "", new IncidentStatusRequest("Investigating"), version, "key"), 400);
    }
}
