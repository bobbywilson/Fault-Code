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
    private static CreateDriverRequest DriverBody(string identifier = "DRV-001", string owner = Actor) => new(identifier, owner, "Demo Driver", "driver@example.invalid", true);
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
        var caller = new FleetCaller(Tenant, Actor, Client, "demo-correlation", true, false, true, false);
        var creates = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => service.CreateDriver(caller, new("DRV-001", Actor, "Demo", null, false), "same", default))));
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
    public async Task Invalid_demo_driver_input_returns_safe_validation(string identifier, string owner, string? email, bool notify)
    {
        using var keys = new Discovery(); await using var factory = CreateFactory(keys); using var client = factory.CreateClient(); Authenticate(client, keys);
        using var request = Request(HttpMethod.Post, "/api/v1/drivers", new CreateDriverRequest(identifier, owner, "Demo", email, notify), key: "invalid");
        var json = await Json(await client.SendAsync(request), 400); Assert.DoesNotContain(owner, json.GetRawText());
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
