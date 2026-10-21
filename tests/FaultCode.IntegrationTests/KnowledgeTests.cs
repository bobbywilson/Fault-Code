using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FaultCode.Api.Contracts;
using FaultCode.Application.Knowledge;
using FaultCode.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static FaultCode.IntegrationTests.EntraAuthenticationTests;

namespace FaultCode.IntegrationTests;
// Test-only adapter: one process, copy-on-write; no production durability claim.
internal sealed class TestKnowledgeStore : IKnowledgeStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, KnowledgeState> tenants = [];
    public bool Fail { get; set; }
    public Task<KnowledgeState> ReadAsync(string tenant, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); lock (gate) return Task.FromResult(tenants.TryGetValue(tenant, out var s) ? s.Copy() : new KnowledgeState()); }
    public Task<T> TransactAsync<T>(string tenant, Func<KnowledgeState, T> operation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); lock (gate)
        {
            var working = tenants.TryGetValue(tenant, out var s) ? s.Copy() : new KnowledgeState();
            var result = operation(working); ct.ThrowIfCancellationRequested();
            if (Fail) throw new KnowledgeException(503);
            tenants[tenant] = working.Copy(); return Task.FromResult(result);
        }
    }
}
internal sealed class TestTaxonomy : IFaultTaxonomy
{
    public bool Unavailable { get; set; }
    public Task<bool> ValidateAsync(string tenant, string system, FaultContent content, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); if (Unavailable) throw new KnowledgeException(503);
        return Task.FromResult(content.ClassificationId == "synthetic-" + system && content.ClassificationVersion == 1
            && content.Remedies.All(x => x.Kind is "guidance" or "dealership"));
    }
}
public sealed class KnowledgeTests
{
    private static readonly KnowledgeCaller Caller = new(Tenant, Actor, Client, "synthetic-correlation");
    private static FaultContentRequest Content(string system = "EPS") => new("Synthetic fault", "synthetic-" + system, 1,
        ["Synthetic reduced assist"], [new("SYN-DOC-1", "1")],
        [new("one", "dealership", "Synthetic dealership recommendation", true, "SYN-MODEL-1", "1.0", new("SYN-PATCH-1", "1.0")),
         new("two", "dealership", "Unapproved patch", false, "SYN-MODEL-1", "1.0", new("SYN-PATCH-2", "1.0")),
         new("three", "guidance", "Other model", true, "SYN-MODEL-2", "1.0", null)]);
    private static void Auth(HttpClient client, Discovery keys, string scope = "faults.write faults.read", string role = "Operator", bool machine = false) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthorizationTests.Token(keys.Key, machine ? null : scope, machine ? scope.Split(' ') : [role], machine));
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
            Assert.Equal((HttpStatusCode)status, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (status >= 400) { Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType); Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), json.RootElement.GetProperty("correlationId").GetString()); }
            else if (request.Method != HttpMethod.Get || !request.RequestUri!.OriginalString.EndsWith("history", StringComparison.Ordinal))
            { if (json.RootElement.TryGetProperty("version", out _)) Assert.NotNull(response.Headers.ETag); }
            if (status == 201) Assert.NotNull(response.Headers.Location);
            return json.RootElement.Clone();
        }
    }
    private const string Lookup = "/api/v1/faults/lookup?code=SYN-EPS:001&system=EPS&model=SYN-MODEL-1&software=1.0";
    [Fact]
    public async Task Production_controllers_crud_publication_history_and_approved_filtering()
    {
        using var keys = new Discovery(); var store = new TestKnowledgeStore(); var taxonomy = new TestTaxonomy();
        await using var factory = CreateFactory(keys).WithWebHostBuilder(b => b.ConfigureServices(s => { s.Replace(ServiceDescriptor.Singleton<IKnowledgeStore>(store)); s.Replace(ServiceDescriptor.Singleton<IFaultTaxonomy>(taxonomy)); }));
        using var client = factory.CreateClient(); Auth(client, keys);
        var body = new CreateFaultRequest(" syn-eps:001 ", "EPS", Content());
        var created = await Send(client, Request(HttpMethod.Post, "/api/v1/faults", body, key: "create"), 201); var id = created.GetProperty("id").GetGuid(); var path = "/api/v1/faults/" + id;
        var replay = await Send(client, Request(HttpMethod.Post, "/api/v1/faults", body with { Code = "SYN-EPS:001" }, key: "create"), 201); Assert.Equal(created.GetRawText(), replay.GetRawText());
        await Send(client, Request(HttpMethod.Post, "/api/v1/faults", body with { Code = "SYN-EPS:002" }, key: "create"), 409);
        await Send(client, Request(HttpMethod.Post, "/api/v1/faults", body, key: "duplicate"), 409);
        var get = await Send(client, Request(HttpMethod.Get, path)); Assert.Equal("SYN-EPS:001", get.GetProperty("code").GetString()); Assert.False(get.TryGetProperty("tenantId", out _));
        var list = await Send(client, Request(HttpMethod.Get, "/api/v1/faults?system=EPS&search=synthetic&pageSize=1")); Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        await Send(client, Request(HttpMethod.Post, path + "/publications", version: "\"1\"", key: "publish"), 403);
        Auth(client, keys, "knowledge.read", "Reader"); await Send(client, Request(HttpMethod.Get, Lookup), 404);
        Auth(client, keys, "knowledge.publish", "Engineer"); await Send(client, Request(HttpMethod.Post, path + "/publications", version: "\"1\"", key: "publish"));
        taxonomy.Unavailable = true; await Send(client, Request(HttpMethod.Post, path + "/publications", version: "\"1\"", key: "publish")); taxonomy.Unavailable = false;
        await Send(client, Request(HttpMethod.Post, path + "/publications", version: "\"2\"", key: "publish"), 409);
        Auth(client, keys, "knowledge.read", "Reader");
        var lookup = await Send(client, Request(HttpMethod.Get, Lookup)); Assert.Equal(2, lookup.GetProperty("version").GetInt64()); Assert.Equal(Actor, lookup.GetProperty("approvedBy").GetString());
        var remedies = lookup.GetProperty("content").GetProperty("remedies"); Assert.Single(remedies.EnumerateArray()); Assert.Equal("one", remedies[0].GetProperty("id").GetString());
        var none = await Send(client, Request(HttpMethod.Get, Lookup.Replace("software=1.0", "software=2.0", StringComparison.Ordinal))); Assert.Empty(none.GetProperty("content").GetProperty("remedies").EnumerateArray());
        await Send(client, Request(HttpMethod.Get, path), 403); // Knowledge-only consumers cannot inspect drafts.
        Auth(client, keys); var edited = Content() with { Description = "New draft only" };
        await Send(client, Request(HttpMethod.Put, path, edited, "\"2\""));
        await Send(client, Request(HttpMethod.Put, path, edited, "\"2\""), 409);
        Auth(client, keys, "knowledge.read", "Reader"); Assert.Equal("Synthetic fault", (await Send(client, Request(HttpMethod.Get, Lookup))).GetProperty("content").GetProperty("description").GetString());
        Auth(client, keys); var history = await Send(client, Request(HttpMethod.Get, path + "/history")); Assert.Equal(3, history.GetProperty("totalCount").GetInt32());
        await Send(client, Request(HttpMethod.Delete, path, version: "\"3\"")); await Send(client, Request(HttpMethod.Put, path, edited, "\"4\""), 409);
        await Send(client, Request(HttpMethod.Post, "/api/v1/faults", body, key: "reserved"), 409);
        Auth(client, keys, "knowledge.read", "Reader"); await Send(client, Request(HttpMethod.Get, Lookup), 404);
        var state = await store.ReadAsync(Tenant, default); Assert.Equal(4, state.Audit.Count); Assert.Equal(4, state.Faults.Single().History.Length); Assert.Single(state.Faults.Single().Publications);
        Assert.All(state.Audit, a => { Assert.Equal(Actor, a.ActorId); Assert.Equal(Client, a.ClientId); Assert.NotEmpty(a.CorrelationId); });
    }
    [Fact]
    public async Task Concurrent_retry_update_publish_rollback_cancel_and_tenant_contract()
    {
        var store = new TestKnowledgeStore(); var service = new KnowledgeService(store, new TestTaxonomy(), TimeProvider.System);
        var input = new FaultCreate("SYN-EPS:001", "EPS", Content().ToDomain());
        var creates = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => service.Create(Caller, input, "create", default)))); Assert.Single(creates.Distinct());
        var id = creates[0].Id;
        var publishes = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => service.Publish(Caller, id, 1, "publish", default)))); Assert.Single(publishes.Distinct());
        async Task<int> Update() { try { await service.Update(Caller, id, input.Content, 2, default); return 200; } catch (KnowledgeException e) { return e.Status; } }
        var updates = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(Update))); Assert.Equal(1, updates.Count(x => x == 200)); Assert.Equal(7, updates.Count(x => x == 409));
        store.Fail = true; await Assert.ThrowsAsync<KnowledgeException>(() => service.Retire(Caller, id, 3, default));
        var state = await store.ReadAsync(Tenant, default); Assert.False(state.Faults.Single().Retired); Assert.Equal(3, state.Audit.Count); Assert.Equal(2, state.Replays.Count);
        await Assert.ThrowsAsync<KnowledgeException>(() => service.Create(Caller, input with { Code = "SYN-EPS:002" }, "rollback", default)); Assert.Single((await store.ReadAsync(Tenant, default)).Faults);
        store.Fail = false; using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.Create(Caller, input, "cancel", cancelled.Token));
        var other = Caller with { TenantId = "55555555-5555-4555-8555-555555555555" };
        Assert.Equal(404, (await Assert.ThrowsAsync<KnowledgeException>(() => service.Get(other, id, default))).Status); Assert.Empty((await service.List(other, 1, 20, null, null, null, default)).Items);
        state.Faults.Clear(); Assert.Single((await store.ReadAsync(Tenant, default)).Faults);
        Assert.Equal(2, (await service.Lookup(Caller, input.Code, input.System, "SYN-MODEL-1", "1.0", default)).Version);
    }
    [Theory]
    [InlineData("EPS", "SYN-EPS:001")]
    [InlineData("steer-by-wire", "SYN-SBW:001")]
    [InlineData("steering-ADAS", "SYN-ADAS:001")]
    public async Task Synthetic_systems_and_invalid_content_taxonomy_are_validated(string system, string code)
    {
        var store = new TestKnowledgeStore(); var taxonomy = new TestTaxonomy(); var service = new KnowledgeService(store, taxonomy, TimeProvider.System);
        var content = Content(system).ToDomain(); await service.Create(Caller, new(code, system, content), "valid", default);
        var invalid = new[] { content with { ClassificationId = "unknown" }, content with { ClassificationVersion = 2 }, content with { ClassificationId = "synthetic-other-system" },
            content with { Remedies = [content.Remedies[0] with { Kind = "guidance" }] }, content with { Remedies = [content.Remedies[0], content.Remedies[0]] },
            content with { Remedies = [content.Remedies[0] with { Patch = new("SYN-PATCH", "9.0") }] }, content with { Symptoms = [] }, content with { References = [] }, content with { Description = "" } };
        foreach (var item in invalid) Assert.Equal(400, (await Assert.ThrowsAsync<KnowledgeException>(() => service.Create(Caller, new(code + "X", system, item), "invalid", default))).Status);
        var id = (await store.ReadAsync(Tenant, default)).Faults.Single().Id; taxonomy.Unavailable = true; Assert.Equal(503, (await Assert.ThrowsAsync<KnowledgeException>(() => service.Publish(Caller, id, 1, "publish", default))).Status);
        Assert.Single((await store.ReadAsync(Tenant, default)).Audit);
    }
    [Fact]
    public async Task Republish_preserves_exact_old_snapshots_and_unknown_is_distinct_from_dependency_outage()
    {
        var store = new TestKnowledgeStore(); var taxonomy = new TestTaxonomy(); var service = new KnowledgeService(store, taxonomy, TimeProvider.System);
        var original = Content().ToDomain(); var id = (await service.Create(Caller, new("SYN-EPS:001", "EPS", original), "create", default)).Id;
        await service.Publish(Caller, id, 1, "publish-1", default);
        var replacement = original with { Description = "Second approved revision", Remedies = [] };
        await service.Update(Caller, id, replacement, 2, default); await service.Publish(Caller, id, 3, "publish-2", default);
        var f = await service.Get(Caller, id, default); Assert.Equal(2, f.Publications.Length); Assert.Equal(original, f.Publications[0].Content); Assert.Equal(replacement, f.Publications[1].Content);
        var lookup = await service.Lookup(Caller, "SYN-EPS:001", "EPS", "SYN-MODEL-1", "1.0", default); Assert.Equal(4, lookup.Version); Assert.Empty(lookup.Content.Remedies);
        Assert.Equal(404, (await Assert.ThrowsAsync<KnowledgeException>(() => service.Lookup(Caller, "SYN-EPS:UNKNOWN", "EPS", "SYN-MODEL-1", "1.0", default))).Status);
        var unavailable = new KnowledgeService(new FaultCode.Infrastructure.UnavailableKnowledgeStore(), taxonomy, TimeProvider.System);
        Assert.Equal(503, (await Assert.ThrowsAsync<KnowledgeException>(() => unavailable.Lookup(Caller, "SYN-EPS:UNKNOWN", "EPS", "SYN-MODEL-1", "1.0", default))).Status);
        await Assert.ThrowsAsync<KnowledgeException>(() => store.TransactAsync<int>(Tenant, state => { state.Faults.Clear(); throw new KnowledgeException(409); }, default));
        Assert.Single((await store.ReadAsync(Tenant, default)).Faults);
        using var cancel = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.TransactAsync(Tenant, state => { state.Faults.Clear(); cancel.Cancel(); return 0; }, cancel.Token));
        Assert.Single((await store.ReadAsync(Tenant, default)).Faults);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    [InlineData("W/\"1\"")]
    [InlineData("\"01\"")]
    [InlineData("*")]
    [InlineData("\"0\"")]
    [InlineData("\"1\",\"2\"")]
    public async Task Strong_preconditions_are_required(string? version)
    {
        using var keys = new Discovery(); await using var factory = CreateFactory(keys); using var client = factory.CreateClient(); Auth(client, keys);
        await Send(client, Request(HttpMethod.Delete, "/api/v1/faults/" + Guid.NewGuid(), version: version), 400);
    }
    [Fact]
    public async Task Production_unavailable_exact_permissions_and_safe_invalid_input()
    {
        using var keys = new Discovery(); await using var factory = CreateFactory(keys); using var client = factory.CreateClient();
        await Send(client, Request(HttpMethod.Get, "/api/v1/faults"), 401);
        Auth(client, keys, "faults.write", "Administrator"); await Send(client, Request(HttpMethod.Post, "/api/v1/faults", new CreateFaultRequest("SYN-EPS:001", "EPS", Content()), key: "create"), 403);
        Auth(client, keys, "knowledge.publish", "Operator"); await Send(client, Request(HttpMethod.Post, "/api/v1/faults/" + Guid.NewGuid() + "/publications", version: "\"1\"", key: "publish"), 403);
        Auth(client, keys, "faults.read", "Reader"); await Send(client, Request(HttpMethod.Get, "/api/v1/faults"), 503);
        Auth(client, keys, "Faults.Read.All", machine: true); await Send(client, Request(HttpMethod.Get, "/api/v1/faults"), 503);
        Auth(client, keys, "Knowledge.Read.All", machine: true); await Send(client, Request(HttpMethod.Get, Lookup), 503);
        Auth(client, keys, "Knowledge.Publish", machine: true); await Send(client, Request(HttpMethod.Post, "/api/v1/faults/" + Guid.NewGuid() + "/publications", version: "\"1\"", key: "publish"), 503);
        Auth(client, keys, "Faults.Write.All", machine: true); await Send(client, Request(HttpMethod.Post, "/api/v1/faults", new CreateFaultRequest("SYN-EPS:001", "EPS", Content()), key: "create"), 503);
        Auth(client, keys);
        foreach (var key in new string?[] { null, "", "bad key", "bad/key" }) await Send(client, Request(HttpMethod.Post, "/api/v1/faults", new CreateFaultRequest("SYN-EPS:001", "EPS", Content()), key: key), 400);
        await Send(client, Request(HttpMethod.Post, "/api/v1/faults", new CreateFaultRequest("real-private-code", "EPS", Content()), key: "invalid"), 400);
        await Send(client, Request(HttpMethod.Post, "/api/v1/faults", new { code = "SYN-EPS:001", system = "EPS", content = Content(), approvedBy = Actor }, key: "spoof"), 400);
        await Send(client, Request(HttpMethod.Get, "/api/v1/faults?pageSize=101"), 400);
        await Send(client, Request(HttpMethod.Get, "/api/v1/faults?system=brakes"), 400);
    }
}
