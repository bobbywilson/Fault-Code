using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FaultCode.Application.Fleet;
using FaultCode.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static FaultCode.IntegrationTests.EntraAuthenticationTests;

namespace FaultCode.IntegrationTests;

// Test-only store. No production assembly registers or references this type.
internal sealed class TestFleetStore : IFleetStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, FleetState> tenants = [];
    public bool FailBeforeCommit { get; set; }
    public Task<FleetState> ReadAsync(string tenantId, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); lock (gate) return Task.FromResult(tenants.TryGetValue(tenantId, out var s) ? s.Copy() : new FleetState()); }
    public Task<T> TransactAsync<T>(string tenantId, Func<FleetState, T> op, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (gate)
        {
            var working = tenants.TryGetValue(tenantId, out var s) ? s.Copy() : new FleetState();
            var result = op(working); ct.ThrowIfCancellationRequested();
            if (FailBeforeCommit) throw new FleetException(503);
            tenants[tenantId] = working.Copy(); return Task.FromResult(result);
        }
    }
}
public sealed class FleetTests
{
    private const string Other = "55555555-5555-4555-8555-555555555555";
    private static void Authenticate(HttpClient client, Discovery keys, string scope = "drivers.write vehicles.write drivers.read vehicles.read", string role = "Operator", string actor = Actor, bool machine = false) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthorizationTests.Token(keys.Key, machine ? null : scope,
            machine ? scope.Split(' ') : [role], machine, actor));
    private static HttpRequestMessage Request(HttpMethod method, string path, object? body = null, string? version = null, string? key = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (version is not null) request.Headers.TryAddWithoutValidation("If-Match", version);
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return request;
    }
    private static async Task<MutationResponse> Create(HttpClient client, string area, object body, string key)
    {
        using var request = Request(HttpMethod.Post, "/api/v1/" + area, body, key: key);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("\"1\"", response.Headers.ETag!.ToString());
        Assert.NotNull(response.Headers.Location);
        return (await response.Content.ReadFromJsonAsync<MutationResponse>())!;
    }
    private static CreateDriverRequest DriverBody(string identifier = "DRV-001", string owner = Actor) => new(identifier, owner, "Synthetic Driver", "driver@example.invalid", true);
    private static async Task<JsonElement> Json(HttpResponseMessage response, int status = 200)
    {
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (status >= 400)
        {
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), json.RootElement.GetProperty("correlationId").GetString());
        }
        return json.RootElement.Clone();
    }
    [Fact]
    public async Task Many_to_many_pairs_preserve_independent_history_and_reject_duplicate_pairs()
    {
        using var keys = new Discovery(); var store = new TestFleetStore();
        await using var factory = CreateFactory(keys).WithWebHostBuilder(b => b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IFleetStore>(store))));
        using var client = factory.CreateClient(); Authenticate(client, keys);
        var first = await Create(client, "drivers", DriverBody("DRV-101"), "driver-101");
        var second = await Create(client, "drivers", DriverBody("DRV-102", Other), "driver-102");
        var vehicle = await Create(client, "vehicles", new VehicleRequest("SYN-101", "EPS"), "vehicle-101");
        var otherVehicle = await Create(client, "vehicles", new VehicleRequest("SYN-102", "EPS"), "vehicle-102");
        using (var request = Request(HttpMethod.Post, $"/api/v1/vehicles/{vehicle.Id}/associations", new AssociationRequest(first.Id), "\"1\"", "pair-1")) await Json(await client.SendAsync(request), 201);
        using (var request = Request(HttpMethod.Post, $"/api/v1/vehicles/{vehicle.Id}/associations", new AssociationRequest(second.Id), "\"2\"", "pair-2")) await Json(await client.SendAsync(request), 201);
        using (var request = Request(HttpMethod.Post, $"/api/v1/vehicles/{otherVehicle.Id}/associations", new AssociationRequest(first.Id), "\"1\"", "pair-3")) await Json(await client.SendAsync(request), 201);
        using (var request = Request(HttpMethod.Post, $"/api/v1/vehicles/{vehicle.Id}/associations", new AssociationRequest(first.Id), "\"3\"", "duplicate")) await Json(await client.SendAsync(request), 409);
        var before = await store.ReadAsync(Tenant, default); Assert.Equal(3, before.Associations.Count);
        var pair = before.Associations.Single(a => a.VehicleId == vehicle.Id && a.DriverId == first.Id);
        var service = new FleetService(store, TimeProvider.System);
        await service.EndAssociation(new(Tenant, Actor, Client, "many-to-many", true, false, true, false), vehicle.Id, pair.Id, 3, default);
        var after = await store.ReadAsync(Tenant, default);
        Assert.NotNull(after.Associations.Single(a => a.Id == pair.Id).Until);
        Assert.Equal(2, after.Associations.Count(a => a.Until is null));
        Assert.Contains(after.Associations, a => a.VehicleId == vehicle.Id && a.DriverId == second.Id && a.Until is null);
        Assert.Contains(after.Associations, a => a.VehicleId == otherVehicle.Id && a.DriverId == first.Id && a.Until is null);
    }

    [Fact]
    public async Task Crud_replay_concurrency_and_history_preserve_atomic_audit()
    {
        using var keys = new Discovery(); var store = new TestFleetStore();
        await using var factory = CreateFactory(keys).WithWebHostBuilder(b => b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IFleetStore>(store))));
        using var client = factory.CreateClient(); Authenticate(client, keys);
        var driver = await Create(client, "drivers", DriverBody(" drv-001 "), "driver-1");
        var replay = await Create(client, "drivers", DriverBody("DRV-001"), "driver-1"); Assert.Equal(driver, replay);
        var vehicle = await Create(client, "vehicles", new VehicleRequest(" syn-001 ", "EPS"), "vehicle-1");
        using (var conflict = Request(HttpMethod.Post, "/api/v1/drivers", DriverBody("DRV-002"), key: "driver-1")) await Json(await client.SendAsync(conflict), 409);
        using (var duplicate = Request(HttpMethod.Post, "/api/v1/drivers", DriverBody("DRV-001", Other), key: "duplicate")) await Json(await client.SendAsync(duplicate), 409);
        await Json(await client.GetAsync("/api/v1/vehicles/" + vehicle.Id), 404); // No trusted owner yet.
        using (var associate = Request(HttpMethod.Post, $"/api/v1/vehicles/{vehicle.Id}/associations", new AssociationRequest(driver.Id), "\"1\"", "association-1"))
        { var response = await client.SendAsync(associate); await Json(response, 201); Assert.Equal("\"2\"", response.Headers.ETag!.ToString()); }
        using (var repeat = Request(HttpMethod.Post, $"/api/v1/vehicles/{vehicle.Id}/associations", new AssociationRequest(driver.Id), "\"1\"", "association-1")) await Json(await client.SendAsync(repeat), 201);
        using (var duplicate = Request(HttpMethod.Post, $"/api/v1/vehicles/{vehicle.Id}/associations", new AssociationRequest(driver.Id), "\"2\"", "association-2")) await Json(await client.SendAsync(duplicate), 409);
        var body = await Json(await client.GetAsync("/api/v1/vehicles/" + vehicle.Id)); Assert.Equal("SYN-001", body.GetProperty("identifier").GetString()); Assert.False(body.TryGetProperty("ownerObjectId", out _));
        using (var stale = Request(HttpMethod.Put, "/api/v1/vehicles/" + vehicle.Id, new VehicleRequest("SYN-002", "EPS"), "\"1\"")) await Json(await client.SendAsync(stale), 409);
        using (var update = Request(HttpMethod.Put, "/api/v1/drivers/" + driver.Id, new UpdateDriverRequest("Updated Synthetic", null, false), "\"1\"")) await Json(await client.SendAsync(update));
        using (var deactivate = Request(HttpMethod.Delete, "/api/v1/drivers/" + driver.Id, version: "\"2\"")) await Json(await client.SendAsync(deactivate));
        var history = await Json(await client.GetAsync($"/api/v1/vehicles/{vehicle.Id}/associations")); Assert.Equal(1, history.GetProperty("totalCount").GetInt32()); Assert.NotEqual(JsonValueKind.Null, history.GetProperty("items")[0].GetProperty("until").ValueKind);
        using (var invalid = Request(HttpMethod.Post, $"/api/v1/vehicles/{vehicle.Id}/associations", new AssociationRequest(driver.Id), "\"3\"", "inactive")) await Json(await client.SendAsync(invalid), 409);
        var state = await store.ReadAsync(Tenant, default); Assert.Equal(5, state.Audit.Count); Assert.Equal(3, state.Replays.Count);
        Assert.All(state.Audit, a => { Assert.Equal(Actor, a.ActorId); Assert.Equal(Client, a.ClientId); Assert.NotEmpty(a.CorrelationId); });
        Assert.Equal(3, state.Vehicles.Single().Version); Assert.False(state.Drivers.Single().Active);
    }
    [Fact]
    public async Task Ownership_filters_before_count_and_page_and_does_not_accept_spoofed_mapping()
    {
        using var keys = new Discovery(); var store = new TestFleetStore();
        await using var factory = CreateFactory(keys).WithWebHostBuilder(b => b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IFleetStore>(store))));
        using var client = factory.CreateClient(); Authenticate(client, keys);
        var one = await Create(client, "drivers", DriverBody(), "one"); var two = await Create(client, "drivers", DriverBody("DRV-002", Other), "two");
        var v = await Create(client, "vehicles", new VehicleRequest("SYN-001", "EPS"), "vehicle");
        using (var associate = Request(HttpMethod.Post, $"/api/v1/vehicles/{v.Id}/associations", new AssociationRequest(one.Id), "\"1\"", "association")) await Json(await client.SendAsync(associate), 201);
        Authenticate(client, keys, "drivers.read.own vehicles.read.own", "Reader");
        var list = await Json(await client.GetAsync("/api/v1/drivers?pageSize=1&ownerObjectId=" + Other)); Assert.Equal(1, list.GetProperty("totalCount").GetInt32()); Assert.Equal(one.Id, list.GetProperty("items")[0].GetProperty("id").GetGuid());
        var empty = await Json(await client.GetAsync("/api/v1/drivers?page=2&pageSize=1")); Assert.Equal(1, empty.GetProperty("totalCount").GetInt32()); Assert.Empty(empty.GetProperty("items").EnumerateArray());
        await Json(await client.GetAsync("/api/v1/drivers/" + two.Id), 404); await Json(await client.GetAsync("/api/v1/drivers/" + one.Id)); await Json(await client.GetAsync("/api/v1/vehicles/" + v.Id));
        Authenticate(client, keys, "drivers.read.own vehicles.read.own", "Reader", Other);
        await Json(await client.GetAsync("/api/v1/vehicles/" + v.Id), 404);
        Assert.Equal(0, (await Json(await client.GetAsync("/api/v1/vehicles"))).GetProperty("totalCount").GetInt32());
        using (var write = Request(HttpMethod.Post, "/api/v1/drivers", DriverBody(), key: "forbidden")) await Json(await client.SendAsync(write), 403);
        Authenticate(client, keys, "drivers.read vehicles.read", "Administrator");
        Assert.Equal(2, (await Json(await client.GetAsync("/api/v1/drivers"))).GetProperty("totalCount").GetInt32());
        using (var write = Request(HttpMethod.Delete, "/api/v1/drivers/" + one.Id, version: "\"1\"")) await Json(await client.SendAsync(write), 403);
        Authenticate(client, keys, "Drivers.Read.All Vehicles.Read.All", machine: true);
        await Json(await client.GetAsync("/api/v1/drivers/" + two.Id)); await Json(await client.GetAsync("/api/v1/vehicles/" + v.Id));
    }
    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    [InlineData("W/\"1\"")]
    [InlineData("\"0\"")]
    [InlineData("\"01\"")]
    [InlineData("*")]
    [InlineData("\"1\",\"2\"")]
    public async Task Mutation_preconditions_fail_closed(string? version)
    {
        using var keys = new Discovery();
        await using var factory = CreateFactory(keys); using var client = factory.CreateClient(); Authenticate(client, keys);
        using var request = Request(HttpMethod.Put, "/api/v1/vehicles/" + Guid.NewGuid(), new VehicleRequest("SYN-001", "EPS"), version);
        await Json(await client.SendAsync(request), 400);
    }
    [Fact]
    public async Task Production_storage_is_unavailable_and_permissions_are_checked_first()
    {
        using var keys = new Discovery(); await using var factory = CreateFactory(keys); using var client = factory.CreateClient();
        foreach (var path in new[] { "/api/v1/drivers", "/api/v1/vehicles" }) await Json(await client.GetAsync(path), 401);
        Authenticate(client, keys, "authorization.read", "Reader"); await Json(await client.GetAsync("/api/v1/drivers"), 403);
        Authenticate(client, keys); await Json(await client.GetAsync("/api/v1/drivers"), 503);
        using var request = Request(HttpMethod.Post, "/api/v1/drivers", DriverBody(), key: "no-store"); await Json(await client.SendAsync(request), 503);
    }
    [Fact]
    public async Task Concurrent_creates_updates_rollback_and_tenant_isolation_obey_store_contract()
    {
        var store = new TestFleetStore(); var service = new FleetService(store, TimeProvider.System);
        var caller = new FleetCaller(Tenant, Actor, Client, "synthetic-correlation", true, false, true, false);
        var creates = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => service.CreateDriver(caller, new("DRV-001", Actor, "Synthetic", null, false), "same", default))));
        Assert.Single(creates.Distinct()); Assert.Single((await store.ReadAsync(Tenant, default)).Audit);
        var driver = creates[0];
        async Task<int> Update() { try { await service.UpdateDriver(caller, driver.Id, new("Updated", null, false), 1, default); return 200; } catch (FleetException e) { return e.Status; } }
        var updates = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(Update)));
        Assert.Equal(1, updates.Count(x => x == 200)); Assert.Equal(7, updates.Count(x => x == 409));
        store.FailBeforeCommit = true;
        await Assert.ThrowsAsync<FleetException>(() => service.CreateVehicle(caller, new("SYN-001", "EPS"), "rollback", default));
        var state = await store.ReadAsync(Tenant, default); Assert.Empty(state.Vehicles); Assert.Equal(2, state.Audit.Count); Assert.Single(state.Replays);
        store.FailBeforeCommit = false;
        var other = caller with { TenantId = Other };
        Assert.Equal(404, (await Assert.ThrowsAsync<FleetException>(() => service.GetDriver(other, driver.Id, default))).Status);
        Assert.Empty((await service.Drivers(other, 1, 20, null, default)).Items);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CreateVehicle(caller, new("SYN-001", "EPS"), "cancel", cancelled.Token));
        state.Drivers.Clear(); Assert.Single((await store.ReadAsync(Tenant, default)).Drivers);
    }
    [Theory]
    [InlineData("real-vin", Actor, "driver@example.invalid", true)]
    [InlineData("DRV-001", "not-an-owner", "driver@example.invalid", true)]
    [InlineData("DRV-001", Actor, "private@customer.invalid", true)]
    [InlineData("DRV-001", Actor, null, true)]
    public async Task Invalid_synthetic_driver_input_returns_safe_validation(string identifier, string owner, string? email, bool notify)
    {
        using var keys = new Discovery(); await using var factory = CreateFactory(keys); using var client = factory.CreateClient(); Authenticate(client, keys);
        using var request = Request(HttpMethod.Post, "/api/v1/drivers", new CreateDriverRequest(identifier, owner, "Synthetic", email, notify), key: "invalid");
        var json = await Json(await client.SendAsync(request), 400); Assert.DoesNotContain(owner, json.GetRawText());
    }
    [Fact]
    public async Task Vehicle_updates_end_association_deactivation_and_missing_records()
    {
        using var keys = new Discovery(); var store = new TestFleetStore();
        await using var factory = CreateFactory(keys).WithWebHostBuilder(b => b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IFleetStore>(store))));
        using var client = factory.CreateClient(); Authenticate(client, keys);
        var driver = await Create(client, "drivers", DriverBody(), "driver");
        var vehicle = await Create(client, "vehicles", new VehicleRequest("SYN-001", "EPS"), "vehicle");
        using (var update = Request(HttpMethod.Put, "/api/v1/vehicles/" + vehicle.Id, new VehicleRequest("SYN-002", "steer-by-wire"), "\"1\"")) await Json(await client.SendAsync(update));
        using (var missing = Request(HttpMethod.Post, $"/api/v1/vehicles/{vehicle.Id}/associations", new AssociationRequest(Guid.NewGuid()), "\"2\"", "missing-driver")) await Json(await client.SendAsync(missing), 404);
        using (var association = Request(HttpMethod.Post, $"/api/v1/vehicles/{vehicle.Id}/associations", new AssociationRequest(driver.Id), "\"2\"", "link")) await Json(await client.SendAsync(association), 201);
        var history = await Json(await client.GetAsync($"/api/v1/vehicles/{vehicle.Id}/associations")); var aid = history.GetProperty("items")[0].GetProperty("id").GetGuid();
        using (var end = Request(HttpMethod.Delete, $"/api/v1/vehicles/{vehicle.Id}/associations/{aid}", version: "\"3\"")) await Json(await client.SendAsync(end));
        using (var ended = Request(HttpMethod.Delete, $"/api/v1/vehicles/{vehicle.Id}/associations/{aid}", version: "\"4\"")) await Json(await client.SendAsync(ended), 409);
        using (var link = Request(HttpMethod.Post, $"/api/v1/vehicles/{vehicle.Id}/associations", new AssociationRequest(driver.Id), "\"4\"", "relink")) await Json(await client.SendAsync(link), 201);
        using (var deactivate = Request(HttpMethod.Delete, "/api/v1/vehicles/" + vehicle.Id, version: "\"5\"")) await Json(await client.SendAsync(deactivate));
        using (var inactive = Request(HttpMethod.Put, "/api/v1/vehicles/" + vehicle.Id, new VehicleRequest("SYN-003", "EPS"), "\"6\"")) await Json(await client.SendAsync(inactive), 409);
        using (var inactive = Request(HttpMethod.Post, $"/api/v1/vehicles/{vehicle.Id}/associations", new AssociationRequest(driver.Id), "\"6\"", "inactive")) await Json(await client.SendAsync(inactive), 409);
        var retained = await Json(await client.GetAsync($"/api/v1/vehicles/{vehicle.Id}/associations")); Assert.Equal(2, retained.GetProperty("totalCount").GetInt32());
        Assert.All(retained.GetProperty("items").EnumerateArray(), a => Assert.NotEqual(JsonValueKind.Null, a.GetProperty("until").ValueKind));
        await Json(await client.GetAsync("/api/v1/drivers/" + Guid.NewGuid()), 404);
        await Json(await client.GetAsync("/api/v1/drivers?pageSize=101"), 400);
        Assert.Equal(0, (await Json(await client.GetAsync("/api/v1/drivers?active=false"))).GetProperty("totalCount").GetInt32());
        using (var spoof = Request(HttpMethod.Put, "/api/v1/drivers/" + driver.Id, new { displayName = "Synthetic", ownerObjectId = Other }, "\"1\"")) await Json(await client.SendAsync(spoof), 400);
        Authenticate(client, keys, "vehicles.write", "Operator");
        using (var missingGrant = Request(HttpMethod.Post, $"/api/v1/vehicles/{vehicle.Id}/associations", new AssociationRequest(driver.Id), "\"6\"", "missing-grant")) await Json(await client.SendAsync(missingGrant), 403);
        Authenticate(client, keys, "Drivers.Write.All Vehicles.Write.All", machine: true);
        await Create(client, "vehicles", new VehicleRequest("SYN-MACHINE", "steering-ADAS"), "machine");
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid key")]
    [InlineData("invalid/key")]
    public async Task Create_requires_valid_replay_key(string? key)
    {
        using var keys = new Discovery(); await using var factory = CreateFactory(keys); using var client = factory.CreateClient(); Authenticate(client, keys);
        using var request = Request(HttpMethod.Post, "/api/v1/drivers", DriverBody(), key: key);
        await Json(await client.SendAsync(request), 400);
    }

}
