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
    private static readonly KnowledgeCaller Caller = new(Tenant, Actor, Client, "demo-correlation");
    private static OntologyContentRequest Content(string system = "EPS") => new([
        new("SYN-SYM", "symptom", system, "Demo symptom", []),
        new("SYN-CLASS", "classification", system, "Demo classification", ["dealership"]),
        new("SYN-SEV", "severity", system, "Demo severity", [])],
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
    [Theory]
    [InlineData("EPS", "SYN-EPS:001")]
    [InlineData("steer-by-wire", "SYN-SBW:001")]
    [InlineData("steering-ADAS", "SYN-ADAS:001")]
    public async Task Actual_approved_taxonomy_bridge_validates_fault_writes_and_publication(string system, string code)
    {
        var store = new TestOntologyStore(); var service = new OntologyService(store, TimeProvider.System);
        await service.Create(Caller, Content(system).ToDomain(), "create", default);
        var taxonomy = new ApprovedOntologyTaxonomy(store);
        var faultContent = new FaultContent("Demo fault", "SYN-CLASS", 2, ["Demo symptom"], [new("SYN-DOC", "1")],
            [new("SYN-REMEDY", "dealership", "Demo inspection", true, "SYN-MODEL-1", "1.0", null)]);
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
        Assert.False(await new ApprovedOntologyTaxonomy(store).ValidateAsync(other.TenantId, "EPS", new("Demo", "SYN-CLASS", 2, [], [], []), default));
        await Assert.ThrowsAsync<KnowledgeException>(() => store.TransactAsync<int>(Tenant, s => { s.Catalog = null; s.Evaluations.Clear(); throw new KnowledgeException(503); }, default));
        using var cancel = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.TransactAsync(Tenant, s => { s.Catalog = null; cancel.Cancel(); return 0; }, cancel.Token));
        store.Fail = true; await Assert.ThrowsAsync<KnowledgeException>(() => service.Publish(Caller, 3, "failure", default)); store.Fail = false;
        var state = await store.ReadAsync(Tenant, default); Assert.Equal(4, state.Audit.Count); Assert.Equal(3, state.Catalog!.Version); Assert.Single(state.Evaluations);
        state.Evaluations.Clear(); state.Replays.Clear(); state.Catalog = null;
        var again = await store.ReadAsync(Tenant, default); Assert.Single(again.Evaluations); Assert.Equal(3, again.Replays.Count); Assert.NotNull(again.Catalog);
    }
}
