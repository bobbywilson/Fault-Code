using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FaultCode.Api.Contracts;
using FaultCode.Application.Knowledge;
using FaultCode.Application.Ontology;
using FaultCode.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static FaultCode.IntegrationTests.EntraAuthenticationTests;

namespace FaultCode.IntegrationTests;

// Test-only, one process, copy-on-write fixture. This is not durable production persistence.
internal sealed class TestOntologyStore : IOntologyStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, OntologyState> tenants = [];
    public bool Fail { get; set; }
    public Task<OntologyState> ReadAsync(string tenant, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); if (Fail) throw new KnowledgeException(503);
        lock (gate) return Task.FromResult(tenants.TryGetValue(tenant, out var s) ? s.Copy() : new OntologyState());
    }
    public Task<T> TransactAsync<T>(string tenant, Func<OntologyState, T> operation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); lock (gate)
        {
            if (Fail) throw new KnowledgeException(503);
            var state = tenants.TryGetValue(tenant, out var s) ? s.Copy() : new OntologyState();
            var result = operation(state); ct.ThrowIfCancellationRequested(); if (Fail) throw new KnowledgeException(503);
            tenants[tenant] = state.Copy(); return Task.FromResult(result);
        }
    }
}
public sealed class OntologyTests
{
    private static readonly KnowledgeCaller Caller = new(Tenant, Actor, Client, "synthetic-correlation");
    private static OntologyContentRequest Content(string system = "EPS") => new([
        new("SYN-SYM", "symptom", system, "Synthetic symptom", []),
        new("SYN-CLASS", "classification", system, "Synthetic classification", ["dealership"]),
        new("SYN-SEV", "severity", system, "Synthetic severity", [])],
        [new("SYN-EDGE", "indicates", "SYN-SYM", "SYN-CLASS")],
        [new("SYN-RULE", system, ["SYN-SYM"], "SYN-CLASS", "SYN-SEV", ["dealership"], 10, null, null)]);
    private static readonly EvaluateOntologyRequest Input = new(2, "EPS", ["SYN-SYM"], "SYN-MODEL-1", "1.0");
    private static void Auth(HttpClient client, Discovery keys, string scope, string role = "Engineer", bool machine = false) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthorizationTests.Token(keys.Key, machine ? null : scope, machine ? [scope] : [role], machine));
    private static async Task<JsonElement> Send(HttpClient client, HttpMethod method, string path, int status = 200, object? body = null, string? version = null, string? key = null)
    {
        using var request = new HttpRequestMessage(method, "/api/v1/ontology/" + path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (version is not null) request.Headers.TryAddWithoutValidation("If-Match", version);
        if (key is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        using var response = await client.SendAsync(request);
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (status >= 400)
        {
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), json.RootElement.GetProperty("correlationId").GetString());
        }
        else if (!path.StartsWith("history", StringComparison.Ordinal)) Assert.NotNull(response.Headers.ETag);
        if (status == 201) Assert.NotNull(response.Headers.Location);
        Assert.False(json.RootElement.TryGetProperty("tenantId", out _)); return json.RootElement.Clone();
    }
    [Fact]
    public async Task Production_api_manages_publishes_pins_and_retains_rule_results_with_exact_permissions()
    {
        using var keys = new Discovery(); var store = new TestOntologyStore();
        await using var factory = CreateFactory(keys).WithWebHostBuilder(b => b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IOntologyStore>(store))));
        using var client = factory.CreateClient(); Auth(client, keys, "knowledge.publish");
        var content = Content();
        var created = await Send(client, HttpMethod.Post, "draft", 201, content, key: "create");
        var replay = await Send(client, HttpMethod.Post, "draft", 201, content with { Concepts = content.Concepts.Reverse().ToArray() }, key: "create");
        Assert.Equal(created.GetRawText(), replay.GetRawText());
        await Send(client, HttpMethod.Get, "draft");
        await Send(client, HttpMethod.Post, "publications", version: "\"1\"", key: "publish");
        Auth(client, keys, "knowledge.read", "Reader");
        await Send(client, HttpMethod.Get, "draft", 403); await Send(client, HttpMethod.Get, "history", 403);
        await Send(client, HttpMethod.Get, "publications/1", 404);
        var published = await Send(client, HttpMethod.Get, "publications/2"); Assert.Equal(Actor, published.GetProperty("approvedBy").GetString());
        var evaluated = await Send(client, HttpMethod.Post, "evaluations", 201, Input, key: "evaluate");
        var id = evaluated.GetProperty("id").GetGuid(); Assert.Equal("SYN-RULE", evaluated.GetProperty("outcome").GetProperty("ruleId").GetString());
        await Send(client, HttpMethod.Post, "evaluations", 409, Input with { Model = "SYN-MODEL-2" }, key: "evaluate");
        await Send(client, HttpMethod.Post, "evaluations", 400, Input with { Symptoms = ["SYN-MISSING"] }, key: "unknown");
        Auth(client, keys, "knowledge.publish");
        await Send(client, HttpMethod.Post, "publications", version: "\"1\"", key: "publish"); // Exact replay before version check.
        await Send(client, HttpMethod.Post, "publications", 409, version: "\"1\"", key: "stale");
        await Send(client, HttpMethod.Put, "draft", body: content with { Rules = [] }, version: "\"2\"");
        await Send(client, HttpMethod.Put, "draft", 409, content, "\"2\"");
        await Send(client, HttpMethod.Post, "publications", version: "\"3\"", key: "publish-2");
        var history = await Send(client, HttpMethod.Get, "history"); Assert.Equal(4, history.GetProperty("totalCount").GetInt32());
        var bounded = await Send(client, HttpMethod.Get, "history?page=2&pageSize=1"); Assert.Single(bounded.GetProperty("items").EnumerateArray());
        var empty = await Send(client, HttpMethod.Get, "history?page=2147483647&pageSize=100"); Assert.Empty(empty.GetProperty("items").EnumerateArray()); Assert.Equal(4, empty.GetProperty("totalCount").GetInt32());
        Auth(client, keys, "knowledge.read", "Reader");
        await Send(client, HttpMethod.Post, "evaluations", 404, Input with { PublicationVersion = 3 }, key: "draft-version");
        var old = await Send(client, HttpMethod.Get, "evaluations/" + id); Assert.Equal(evaluated.GetRawText(), old.GetRawText());
        var retried = await Send(client, HttpMethod.Post, "evaluations", 201, Input, key: "evaluate"); Assert.Equal(old.GetRawText(), retried.GetRawText());
        var noMatch = await Send(client, HttpMethod.Post, "evaluations", 201, Input with { PublicationVersion = 4 }, key: "new-version");
        Assert.Equal(JsonValueKind.Null, noMatch.GetProperty("outcome").GetProperty("ruleId").ValueKind);
        Assert.Equal(published.GetRawText(), (await Send(client, HttpMethod.Get, "publications/2")).GetRawText());
        var state = await store.ReadAsync(Tenant, default); Assert.Equal(6, state.Audit.Count); Assert.Equal(2, state.Evaluations.Count);
        Assert.All(state.Audit, a => { Assert.Equal(Actor, a.ActorId); Assert.Equal(Client, a.ClientId); Assert.NotEmpty(a.CorrelationId); });
    }
    [Theory]
    [InlineData("EPS", "SYN-EPS:001")]
    [InlineData("steer-by-wire", "SYN-SBW:001")]
    [InlineData("steering-ADAS", "SYN-ADAS:001")]
    public async Task Actual_approved_taxonomy_bridge_validates_fault_writes_and_publication(string system, string code)
    {
        var store = new TestOntologyStore(); var service = new OntologyService(store, TimeProvider.System);
        await service.Create(Caller, Content(system).ToDomain(), "create", default);
        var taxonomy = new ApprovedOntologyTaxonomy(store);
        var faultContent = new FaultContent("Synthetic fault", "SYN-CLASS", 2, ["Synthetic symptom"], [new("SYN-DOC", "1")],
            [new("SYN-REMEDY", "dealership", "Synthetic inspection", true, "SYN-MODEL-1", "1.0", null)]);
        Assert.False(await taxonomy.ValidateAsync(Tenant, system, faultContent, default));
        await service.Publish(Caller, 1, "publish", default);
        var knowledge = new KnowledgeService(new TestKnowledgeStore(), taxonomy, TimeProvider.System);
        var created = await knowledge.Create(Caller, new(code, system, faultContent), "create", default);
        await knowledge.Publish(Caller, created.Id, 1, "publish", default);
        foreach (var invalid in new[] { faultContent with { ClassificationVersion = 1 }, faultContent with { ClassificationId = "SYN-MISSING" },
            faultContent with { Remedies = [faultContent.Remedies[0] with { Kind = "guidance" }] } })
            Assert.Equal(400, (await Assert.ThrowsAsync<KnowledgeException>(() => knowledge.Update(Caller, created.Id, invalid, 2, default))).Status);
        Assert.False(await taxonomy.ValidateAsync(Tenant, system == "EPS" ? "steering-ADAS" : "EPS", faultContent, default));
        await service.Update(Caller, new([new("SYN-OTHER", "subsystem", system, "Replacement", [])], [], []), 2, default);
        await service.Publish(Caller, 3, "second", default);
        Assert.True(await taxonomy.ValidateAsync(Tenant, system, faultContent, default));
        store.Fail = true; Assert.Equal(503, (await Assert.ThrowsAsync<KnowledgeException>(() => taxonomy.ValidateAsync(Tenant, system, faultContent, default))).Status);
    }
    [Fact]
    public async Task Fault_production_controller_uses_registered_approved_ontology_adapter()
    {
        using var keys = new Discovery(); var ontology = new TestOntologyStore(); var knowledge = new TestKnowledgeStore();
        await using var factory = CreateFactory(keys).WithWebHostBuilder(b => b.ConfigureServices(s =>
        { s.Replace(ServiceDescriptor.Singleton<IOntologyStore>(ontology)); s.Replace(ServiceDescriptor.Singleton<IKnowledgeStore>(knowledge)); }));
        using var client = factory.CreateClient(); Auth(client, keys, "knowledge.publish");
        await Send(client, HttpMethod.Post, "draft", 201, Content(), key: "create");
        var content = new FaultContentRequest("Synthetic EPS", "SYN-CLASS", 2, ["Synthetic symptom"], [new("SYN-DOC", "1")], []);
        async Task<HttpResponseMessage> Fault(string code, FaultContentRequest body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/faults") { Content = JsonContent.Create(new CreateFaultRequest(code, "EPS", body)) };
            request.Headers.Add("Idempotency-Key", code.Replace(":", "-", StringComparison.Ordinal)); return await client.SendAsync(request);
        }
        Auth(client, keys, "faults.write", "Operator"); using (var response = await Fault("SYN-EPS:001", content)) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Auth(client, keys, "knowledge.publish"); await Send(client, HttpMethod.Post, "publications", version: "\"1\"", key: "publish");
        Auth(client, keys, "faults.write", "Operator"); using (var response = await Fault("SYN-EPS:001", content)) Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using (var response = await Fault("SYN-EPS:002", content with { ClassificationVersion = 1 })) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using (var response = await Fault("SYN-EPS:003", content with { ClassificationId = "SYN-MISSING" })) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ontology.Fail = true; using (var response = await Fault("SYN-EPS:004", content)) Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Single((await knowledge.ReadAsync(Tenant, default)).Faults);
    }
    [Fact]
    public async Task Concurrent_replay_updates_tenant_isolation_rollback_cancellation_and_detached_snapshots()
    {
        var store = new TestOntologyStore(); var service = new OntologyService(store, TimeProvider.System); var content = Content().ToDomain();
        var created = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => service.Create(Caller, content, "create", default)))); Assert.Single(created.Distinct());
        var published = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => service.Publish(Caller, 1, "publish", default)))); Assert.Single(published.Distinct());
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => service.Evaluate(Caller, Input.ToDomain(), "evaluate", default)))); Assert.Single(results.Select(e => e.Id).Distinct());
        async Task<int> Update() { try { await service.Update(Caller, content, 2, default); return 200; } catch (KnowledgeException e) { return e.Status; } }
        var updated = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(Update))); Assert.Equal(1, updated.Count(x => x == 200)); Assert.Equal(7, updated.Count(x => x == 409));
        Assert.Equal(400, (await Assert.ThrowsAsync<KnowledgeException>(() => service.Update(Caller, content with { Concepts = [.. content.Concepts.Select(c => c.Id == "SYN-SYM" ? c with { Kind = "component" } : c)], Rules = [], Relationships = [] }, 3, default))).Status);
        var other = Caller with { TenantId = "55555555-5555-4555-8555-555555555555" };
        Assert.Equal(404, (await Assert.ThrowsAsync<KnowledgeException>(() => service.Evaluation(other, results[0].Id, default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<KnowledgeException>(() => service.Published(other, 2, default))).Status);
        Assert.False(await new ApprovedOntologyTaxonomy(store).ValidateAsync(other.TenantId, "EPS", new("Synthetic", "SYN-CLASS", 2, [], [], []), default));
        await Assert.ThrowsAsync<KnowledgeException>(() => store.TransactAsync<int>(Tenant, s => { s.Catalog = null; s.Evaluations.Clear(); throw new KnowledgeException(503); }, default));
        using var cancel = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.TransactAsync(Tenant, s => { s.Catalog = null; cancel.Cancel(); return 0; }, cancel.Token));
        store.Fail = true; await Assert.ThrowsAsync<KnowledgeException>(() => service.Publish(Caller, 3, "failure", default)); store.Fail = false;
        var state = await store.ReadAsync(Tenant, default); Assert.Equal(4, state.Audit.Count); Assert.Equal(3, state.Catalog!.Version); Assert.Single(state.Evaluations);
        state.Evaluations.Clear(); state.Replays.Clear(); state.Catalog = null;
        var again = await store.ReadAsync(Tenant, default); Assert.Single(again.Evaluations); Assert.Equal(3, again.Replays.Count); Assert.NotNull(again.Catalog);
    }
    [Fact]
    public async Task All_production_operations_are_unavailable_and_exact_human_machine_permissions_apply()
    {
        using var keys = new Discovery(); await using var factory = CreateFactory(keys); using var client = factory.CreateClient();
        await Send(client, HttpMethod.Get, "draft", 401);
        Auth(client, keys, "knowledge.publish", "Administrator"); await Send(client, HttpMethod.Post, "draft", 403, Content(), key: "create");
        Auth(client, keys, "knowledge.publish", "Operator"); await Send(client, HttpMethod.Put, "draft", 403, Content(), "\"1\"");
        foreach (var machine in new[] { false, true })
        {
            Auth(client, keys, machine ? "Knowledge.Publish.Application" : "knowledge.publish", machine: machine);
            await Send(client, HttpMethod.Get, "draft", 503); await Send(client, HttpMethod.Get, "history", 503);
            await Send(client, HttpMethod.Post, "draft", 503, Content(), key: "create");
            await Send(client, HttpMethod.Put, "draft", 503, Content(), "\"1\"");
            await Send(client, HttpMethod.Post, "publications", 503, version: "\"1\"", key: "publish");
            Auth(client, keys, machine ? "Knowledge.Read.All" : "knowledge.read", "Reader", machine);
            await Send(client, HttpMethod.Get, "publications/2", 503);
            await Send(client, HttpMethod.Post, "evaluations", 503, Input, key: "evaluate");
            await Send(client, HttpMethod.Get, "evaluations/" + Guid.NewGuid(), 503);
        }
    }
    [Fact]
    public async Task Malformed_graphs_preconditions_keys_spoofing_and_bounds_return_safe_400()
    {
        using var keys = new Discovery(); var store = new TestOntologyStore();
        await using var factory = CreateFactory(keys).WithWebHostBuilder(b => b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IOntologyStore>(store))));
        using var client = factory.CreateClient(); Auth(client, keys, "knowledge.publish");
        foreach (var version in new string?[] { null, "1", "W/\"1\"", "\"0\"", "\"01\"", "*", "\"1\",\"2\"" })
            await Send(client, HttpMethod.Put, "draft", 400, Content(), version);
        foreach (var key in new string?[] { null, "bad key", "bad/key", new('a', 101) })
            await Send(client, HttpMethod.Post, "draft", 400, Content(), key: key);
        await Send(client, HttpMethod.Post, "draft", 400, Content() with { Concepts = [null!] }, key: "invalid");
        await Send(client, HttpMethod.Post, "draft", 400, new { concepts = Content().Concepts, relationships = Content().Relationships, rules = Content().Rules, approvedBy = Actor }, key: "spoof");
        await Send(client, HttpMethod.Post, "draft", 400, Content() with { Relationships = [new("SYN-EDGE", "unknown", "SYN-SYM", "SYN-CLASS")] }, key: "bad-relationship");
        await Send(client, HttpMethod.Get, "history?pageSize=101", 400);
        await Send(client, HttpMethod.Post, "draft", 201, Content(), key: "valid");
        await Send(client, HttpMethod.Post, "draft", 409, Content(), key: "another");
        await Send(client, HttpMethod.Post, "draft", 409, Content() with { Rules = [] }, key: "valid");
        Assert.Single((await store.ReadAsync(Tenant, default)).Audit);
    }
}
