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
    private static readonly FleetCaller Caller = new(Tenant, Actor, Client, "synthetic-correlation", true, false, true, false);
    private static IncidentInput Input(string source = "SYN-EVENT-1", string code = "SYN-EPS:001") => new(VehicleId, DriverId, source, code, "EPS", "SYN-MODEL-1", "1.0", Now.AddMinutes(-1));
    private static IncidentContext Context()
    {
        var content = new FaultContent("Approved synthetic fault", "synthetic-EPS", 1, ["Reduced synthetic assist"], [new("SYN-DOC-1", "1")],
            [new("approved", "dealership", "Synthetic inspection", true, "SYN-MODEL-1", "1.0", new("SYN-PATCH-1", "1.0")),
             new("unapproved", "dealership", "Unapproved", false, "SYN-MODEL-1", "1.0", new("SYN-PATCH-2", "1.0")),
             new("other-model", "guidance", "Other model", true, "SYN-MODEL-2", "1.0", null)]);
        return new([new(VehicleId, Tenant, "SYN-VEHICLE-1", "EPS", true, 3)],
            [new(DriverId, Tenant, "DRV-1", Actor, "Synthetic Driver", null, false, true, 1)],
            [new(Guid.NewGuid(), VehicleId, DriverId, Now.AddHours(-2), null)],
            [new(Guid.NewGuid(), Tenant, "SYN-EPS:001", "EPS", false, 3, content with { Description = "Private draft" },
                [new(1, content, "draft", Actor, Now.AddHours(-1))], [new(2, content, Actor, Now.AddHours(-1))])]);
    }
    private static CreateIncidentRequest Body(IncidentInput i) => new(i.VehicleId, i.DriverId, i.SourceEventId, i.Code, i.System, i.Model, i.SoftwareVersion, i.OccurredAt);
    private static void Auth(HttpClient client, Discovery keys, string scope = "faults.write knowledge.read faults.read vehicles.read", string role = "Operator", bool machine = false, string actor = Actor) =>
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
    public async Task Production_controllers_known_unknown_intake_replay_status_failure_retry_and_history()
    {
        using var keys = new Discovery(); var store = new TestIncidentStore(Context()); var clock = new IncidentClock(Now);
        await using var factory = CreateFactory(keys).WithWebHostBuilder(b => b.ConfigureServices(s => { s.Replace(ServiceDescriptor.Singleton<IIncidentStore>(store)); s.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)); }));
        using var client = factory.CreateClient(); Auth(client, keys);
        var input = Input() with { Code = " syn-eps:001 " };
        var created = await Send(client, Request(HttpMethod.Post, "/api/v1/incidents", Body(input), key: "create"), 201); var id = created.GetProperty("id").GetGuid(); var path = "/api/v1/incidents/" + id;
        var replay = await Send(client, Request(HttpMethod.Post, "/api/v1/incidents", Body(input with { Code = "SYN-EPS:001" }), key: "second-key"), 201); Assert.Equal(created.GetRawText(), replay.GetRawText());
        await Send(client, Request(HttpMethod.Post, "/api/v1/incidents", Body(input with { SoftwareVersion = "2.0" }), key: "create"), 409);
        var read = await Send(client, Request(HttpMethod.Get, path)); Assert.Equal("Received", read.GetProperty("status").GetString());
        var known = read.GetProperty("knowledge"); Assert.Equal(2, known.GetProperty("publicationVersion").GetInt64());
        Assert.Equal("Approved synthetic fault", known.GetProperty("content").GetProperty("description").GetString());
        Assert.Single(known.GetProperty("content").GetProperty("remedies").EnumerateArray());
        Assert.Equal("1", known.GetProperty("content").GetProperty("references")[0].GetProperty("version").GetString());
        Assert.False(read.TryGetProperty("tenantId", out _)); Assert.False(read.GetProperty("fleet").TryGetProperty("ownerObjectId", out _));
        await Send(client, Request(HttpMethod.Post, "/api/v1/incidents", Body(Input("SYN-UNKNOWN", "SYN-EPS:UNKNOWN")), key: "unknown"), 201);
        var unmatched = await Send(client, Request(HttpMethod.Get, "/api/v1/incidents?unmatchedOnly=true&pageSize=1"));
        Assert.Equal(1, unmatched.GetProperty("totalCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, unmatched.GetProperty("items")[0].GetProperty("knowledge").ValueKind);
        var list = await Send(client, Request(HttpMethod.Get, "/api/v1/incidents?status=Received&pageSize=1")); Assert.Equal(2, list.GetProperty("totalCount").GetInt32()); Assert.Single(list.GetProperty("items").EnumerateArray());
        await Send(client, Request(HttpMethod.Put, path + "/status", new IncidentStatusRequest("Resolved"), "\"1\"", "illegal"), 409);
        var workId = read.GetProperty("work")[0].GetProperty("id").GetGuid();
        await Send(client, Request(HttpMethod.Post, path + "/failures", new IncidentFailureRequest("dispatch-unavailable"), "\"1\"", "fail"));
        await Send(client, Request(HttpMethod.Post, path + "/failures", new IncidentFailureRequest("dispatch-unavailable"), "\"1\"", "fail"));
        var failed = await Send(client, Request(HttpMethod.Get, path)); Assert.Equal("Failed", failed.GetProperty("work")[0].GetProperty("state").GetString()); Assert.Equal(1, failed.GetProperty("work")[0].GetProperty("attempts").GetInt32());
        await Send(client, Request(HttpMethod.Put, path + "/status", new IncidentStatusRequest("Investigating"), "\"2\"", "blocked"), 409);
        await Send(client, Request(HttpMethod.Post, path + "/retries", version: "\"2\"", key: "retry"));
        await Send(client, Request(HttpMethod.Post, path + "/retries", version: "\"2\"", key: "retry")); // exact replay despite current version
        await Send(client, Request(HttpMethod.Post, path + "/retries", version: "\"3\"", key: "no-failure"), 409);
        var retried = await Send(client, Request(HttpMethod.Get, path)); Assert.Equal(workId, retried.GetProperty("work")[0].GetProperty("id").GetGuid()); Assert.Equal("Pending", retried.GetProperty("work")[0].GetProperty("state").GetString());
        long version = 3;
        foreach (var status in new[] { "Investigating", "Escalated", "Investigating", "Resolved", "NotificationPending", "Notified" })
        {
            await Send(client, Request(HttpMethod.Put, path + "/status", new IncidentStatusRequest(status), $"\"{version}\"", "transition-" + version));
            await Send(client, Request(HttpMethod.Put, path + "/status", new IncidentStatusRequest(status), $"\"{version}\"", "transition-" + version)); version++;
        }
        await Send(client, Request(HttpMethod.Put, path + "/status", new IncidentStatusRequest("Investigating"), $"\"{version}\"", "terminal"), 409);
        var history = await Send(client, Request(HttpMethod.Get, path + "/history?pageSize=2")); Assert.Equal(9, history.GetProperty("totalCount").GetInt32());
        Assert.Equal("failure", history.GetProperty("items")[1].GetProperty("action").GetString());
        var final = (await store.ReadAsync(Tenant, default)).Incidents.Single(i => i.Id == id);
        Assert.Equal(3, final.Work.Length); Assert.All(final.Work, w => Assert.Equal("Completed", w.State));
        Assert.Equal(9, final.History.Length); Assert.Equal("dispatch-unavailable", final.History[1].FailureCode); Assert.Equal("retry", final.History[2].Action);
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
        await Send(client, Request(HttpMethod.Put, "/api/v1/incidents/" + Guid.NewGuid() + "/status", new IncidentStatusRequest("Investigating"), version, "key"), 400);
    }
    [Fact]
    public async Task Real_JWT_policy_ownership_source_input_and_production_unavailable_boundaries()
    {
        using var keys = new Discovery(); var store = new TestIncidentStore(Context());
        await using var factory = CreateFactory(keys).WithWebHostBuilder(b => b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IIncidentStore>(store))));
        using var client = factory.CreateClient();
        await Send(client, Request(HttpMethod.Get, "/api/v1/incidents"), 401);
        Auth(client, keys, "faults.write knowledge.read", "Administrator"); await Send(client, Request(HttpMethod.Post, "/api/v1/incidents", Body(Input()), key: "key"), 403);
        Auth(client, keys, "faults.write"); await Send(client, Request(HttpMethod.Post, "/api/v1/incidents", Body(Input()), key: "key"), 403);
        Auth(client, keys); var created = await Send(client, Request(HttpMethod.Post, "/api/v1/incidents", Body(Input()), key: "key"), 201); var id = created.GetProperty("id").GetGuid(); var path = "/api/v1/incidents/" + id;
        Auth(client, keys, "faults.read", "Reader"); await Send(client, Request(HttpMethod.Get, path), 403);
        Auth(client, keys, "faults.read vehicles.read", "Reader"); await Send(client, Request(HttpMethod.Get, path), 403);
        Auth(client, keys, "faults.read vehicles.read.own knowledge.read", "Reader", actor: Client); await Send(client, Request(HttpMethod.Get, path + "?ownerObjectId=" + Client), 404);
        var empty = await Send(client, Request(HttpMethod.Get, "/api/v1/incidents?ownerObjectId=" + Client)); Assert.Equal(0, empty.GetProperty("totalCount").GetInt32());
        Auth(client, keys, "faults.read vehicles.read.own knowledge.read", "Reader"); await Send(client, Request(HttpMethod.Get, path)); await Send(client, Request(HttpMethod.Get, path + "/history"));
        await Send(client, Request(HttpMethod.Put, path + "/status", new IncidentStatusRequest("Investigating"), "\"1\"", "denied"), 403);
        Auth(client, keys, "Faults.Read.All Vehicles.Read.All Knowledge.Read.All", machine: true); await Send(client, Request(HttpMethod.Get, path));
        Auth(client, keys, "Faults.Write.All Knowledge.Read.All", machine: true);
        await Send(client, Request(HttpMethod.Post, "/api/v1/incidents", Body(Input("machine")), key: "machine"), 201);
        Auth(client, keys);
        foreach (var key in new string?[] { null, "", "bad key", "bad/key" }) await Send(client, Request(HttpMethod.Post, "/api/v1/incidents", Body(Input()), key: key), 400);
        await Send(client, Request(HttpMethod.Post, "/api/v1/incidents", new { vehicleId = VehicleId, driverId = DriverId, sourceEventId = "event", code = "SYN-EPS:001", system = "EPS", model = "SYN-MODEL-1", softwareVersion = "1.0", occurredAt = Now, sourceClientId = Actor }, key: "spoof"), 400);
        await Send(client, Request(HttpMethod.Post, path + "/failures", new IncidentFailureRequest("private-exception-secret"), "\"1\"", "bad"), 400);
        await Send(client, Request(HttpMethod.Get, "/api/v1/incidents?status=invalid"), 400);
        await Send(client, Request(HttpMethod.Get, "/api/v1/incidents?pageSize=101"), 400);
        Auth(client, keys, "faults.read vehicles.read knowledge.read", "Reader"); await Send(client, Request(HttpMethod.Get, "/api/v1/incidents/" + Guid.NewGuid()), 404);
        await using var unavailable = CreateFactory(keys); using var production = unavailable.CreateClient(); Auth(production, keys);
        await Send(production, Request(HttpMethod.Get, "/api/v1/incidents"), 503);
        await Send(production, Request(HttpMethod.Post, "/api/v1/incidents", Body(Input()), key: "production"), 503);
        await Send(production, Request(HttpMethod.Put, path + "/status", new IncidentStatusRequest("Investigating"), "\"1\"", "production"), 503);
    }
}
