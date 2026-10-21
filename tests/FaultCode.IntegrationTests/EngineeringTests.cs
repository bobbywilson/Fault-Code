using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FaultCode.Application.Escalations;
using FaultCode.Application.Agents;
using FaultCode.Application.Investigations;
using FaultCode.Application.Ontology;
using FaultCode.Application.Retrieval;
using FaultCode.Application.Fleet;
using FaultCode.Application.Incidents;
using FaultCode.Application.Knowledge;
using FaultCode.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static FaultCode.IntegrationTests.EntraAuthenticationTests;

namespace FaultCode.IntegrationTests;

// One-process coordinated copy-on-write fixture, never a production durability claim.
internal sealed class TestEngineeringStore : IEscalationStore, IKnowledgeStore, IIncidentStore, IInvestigationStore, IOntologyStore, IDocumentStore
{
    private readonly SemaphoreSlim gate = new(1);
    private readonly AsyncLocal<EngineeringTransaction?> active = new();
    private readonly Dictionary<string, EngineeringTransaction> tenants = [];
    public bool FailCommit { get; set; }
    public Action? BeforeCommit { get; set; }
    private static EngineeringTransaction Copy(EngineeringTransaction t) => new(t.Engineering.Copy(), t.Incidents.Copy(), t.Knowledge.Copy(), t.Ontology.Copy(), t.Documents.Copy(), t.Investigations.Copy());
    public EngineeringTransaction Snapshot(string tenant = Tenant) => tenants.TryGetValue(tenant, out var tx) ? Copy(tx) : new(new(), new(), new(), new(), new(), new());
    public Task<T> Execute<T>(string tenant, Func<EngineeringTransaction, T> action, CancellationToken ct) =>
        ExecuteAsync(tenant, tx => Task.FromResult(action(tx)), ct);
    private async Task<T> ExecuteAsync<T>(string tenant, Func<EngineeringTransaction, Task<T>> action, CancellationToken ct)
    {
        if (active.Value is { } joined) return await action(joined);
        await gate.WaitAsync(ct);
        try
        {
            var tx = Snapshot(tenant); active.Value = tx;
            var result = await action(tx); BeforeCommit?.Invoke(); ct.ThrowIfCancellationRequested();
            if (FailCommit) throw new IncidentException(503);
            tenants[tenant] = Copy(tx); return result;
        }
        finally { active.Value = null; gate.Release(); }
    }
    Task<IncidentState> IIncidentStore.ReadAsync(string tenant, CancellationToken ct) => Execute(tenant, tx => tx.Incidents.Copy(), ct);
    Task<T> IIncidentStore.TransactAsync<T>(string tenant, Func<IncidentTransaction, T> action, CancellationToken ct) =>
        Execute(tenant, tx => action(new(tx.Incidents, () => new([], [], [], tx.Knowledge.Faults))), ct);
    Task<OntologyState> IOntologyStore.ReadAsync(string tenant, CancellationToken ct) => Execute(tenant, tx => tx.Ontology.Copy(), ct);
    Task<T> IOntologyStore.TransactAsync<T>(string tenant, Func<OntologyState, T> action, CancellationToken ct) => Execute(tenant, tx => action(tx.Ontology), ct);
    Task<T> IInvestigationStore.Execute<T>(string tenant, Func<InvestigationState, Task<T>> action, CancellationToken ct) => ExecuteAsync(tenant, tx => action(tx.Investigations), ct);
    Task<T> IDocumentStore.Execute<T>(string tenant, Func<DocumentState, Task<T>> action, CancellationToken ct) => ExecuteAsync(tenant, tx => action(tx.Documents), ct);
    public Task<KnowledgeState> ReadAsync(string tenantId, CancellationToken ct) => Execute(tenantId, t => t.Knowledge.Copy(), ct);
    public Task<T> TransactAsync<T>(string tenantId, Func<KnowledgeState, T> operation, CancellationToken ct) => Execute(tenantId, t => operation(t.Knowledge), ct);
}
public sealed class EngineeringTests
{
    private static readonly Guid IncidentId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid RunId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly FleetCaller Caller = new(Tenant, Actor, Client, "synthetic-engineering", true, false, true, false);
    private static readonly FaultContent Content = new("Synthetic adjudicated EPS finding", "SYN-CLASS", 2, ["SYN-SYM"], [new("SYN-DOC", "1")],
        [new("SYN-REMEDY", "guidance", "Synthetic engineering approved inspection guidance.", true, "SYN-MODEL-1", "1.0", null)]);
    private static EscalationService Service(TestEngineeringStore store) => new(store, TimeProvider.System);
    private static async Task<TestEngineeringStore> Setup(string classification = "UnknownFault")
    {
        var s = new TestEngineeringStore(); var now = DateTimeOffset.UtcNow;
        await s.Execute(Tenant, tx =>
        {
            var i = new Incident(IncidentId, Tenant, Client, "SYN-EVENT", "SYN-EPS:001", "EPS", "SYN-MODEL-1", "1.0", now, now, "intake",
                new(IncidentId, "SYN-VEHICLE", 1, IncidentId, "SYN-DRIVER", 1, Actor, new(IncidentId, IncidentId, IncidentId, now, null)),
                null, "Investigating", 2, [], [new(IncidentId, "investigate", "Pending", 0, null)]);
            tx.Incidents.Incidents.Add(i);
            tx.Investigations.Results.Add(new(Tenant, Actor, Actor, Client, new(RunId, i.Id, 1, now, "synthetic", classification, null, null,
                [], null, ["no-approved-definition"], new("engineering-escalation", "Pending"), new("synthetic", "investigation-v1", 1, 100, 100, 6500, false), "investigation-v1")));
            var ontology = new OntologyContent([new("SYN-CLASS", "classification", "EPS", "Synthetic class", ["guidance", "dealership"])], [], []);
            tx.Ontology.Catalog = new(IncidentId, 2, ontology, [], [new(2, ontology, Actor, now)]);
            var doc = new DocumentContent("Synthetic source", "1", "EPS", "SYN-MODEL-1", "1.0", "tenant", [new(1, "Synthetic canonical engineering source.")]);
            tx.Documents.Documents.Add(new(IncidentId, Tenant, "SYN-DOC", 2, false, doc, [], [new(2, doc, DocumentRules.Checksum(doc), Actor, now)], null));
            return 0;
        }, default); return s;
    }
    private static Task<MutationResult> Create(TestEngineeringStore s, string key = "create") => Service(s).Create(Caller, IncidentId, RunId, 2, key, default);
    private static async Task<Guid> Proposed(TestEngineeringStore s, long expectedFaultVersion = 0)
    {
        var r = await Create(s); await Service(s).Status(Caller, r.Id, 1, "start", "Investigating", default);
        await Service(s).Propose(Caller, r.Id, 2, "propose", Content, expectedFaultVersion, default); return r.Id;
    }
    private static async Task Error(int status, Func<Task> action) => Assert.Equal(status, (await Assert.ThrowsAsync<IncidentException>(action)).Status);

    [Theory]
    [InlineData("UnknownFault")]
    [InlineData("InsufficientEvidence")]
    public async Task Unresolved_cases_deduplicate_concurrently_and_replay_binds_payload(string classification)
    {
        var s = await Setup(classification); var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Create(s)));
        Assert.All(results, r => Assert.Equal(results[0], r));
        Assert.Equal(results[0], await Create(s, "distinct-key"));
        var tx = s.Snapshot(); Assert.Single(tx.Engineering.Cases); Assert.Single(tx.Engineering.Audit); Assert.Single(tx.Engineering.Cases[0].History);
        await Error(409, () => Service(s).Create(Caller, IncidentId, RunId, 1, "create", default));
        await Error(409, () => Service(s).Create(Caller, IncidentId, RunId, 1, "new", default));
        await Error(409, () => Service(s).Create(Caller, IncidentId, Guid.NewGuid(), 2, "create", default));
        await s.Execute(Tenant, t => { t.Investigations.Results.Clear(); return 0; }, default);
        Assert.Equal(results[0], await Create(s)); // committed replay before dependency validation
    }
    [Theory]
    [InlineData("KnownFault")]
    [InlineData("KnownFaultWithApprovedPatch")]
    public async Task Known_routes_cannot_be_used_to_fabricate_unresolved_cases(string classification)
    { var s = await Setup(classification); await Error(409, () => Create(s)); Assert.Empty(s.Snapshot().Engineering.Cases); }
    [Fact]
    public async Task Newly_known_definition_and_provider_outage_do_not_confirm_unknown()
    {
        var s = await Setup(); await s.Execute(Tenant, tx =>
        { tx.Knowledge.Faults.Add(new(IncidentId, Tenant, "SYN-EPS:001", "EPS", false, 1, Content, [], [new(1, Content, Actor, DateTimeOffset.UtcNow)])); return 0; }, default);
        await Error(409, () => Create(s)); Assert.Empty(s.Snapshot().Engineering.Cases);
        var unavailable = new EscalationService(new FaultCode.Infrastructure.UnavailableEscalationStore(), TimeProvider.System);
        await Error(503, () => unavailable.Create(Caller, IncidentId, RunId, 2, "key", default));
        await s.Execute(Tenant, t => { t.Knowledge.Faults.Clear(); t.Investigations.Results.Clear(); return 0; }, default);
        await Error(404, () => Create(s)); Assert.Empty(s.Snapshot().Engineering.Cases);
    }
    [Fact]
    public async Task Assignment_notes_status_and_reopen_preserve_complete_immutable_history()
    {
        var s = await Setup(); var service = Service(s); var r = await Create(s);
        await Error(409, () => service.Status(Caller, r.Id, 1, "bad", "Resolved", default));
        await service.Assign(Caller, r.Id, 1, "assign", Actor, default);
        await service.Note(Caller, r.Id, 2, "note", "Synthetic engineering note", default);
        Assert.Equal(3, (await service.Get(Caller, r.Id, default)).Version);
        await service.Status(Caller, r.Id, 3, "start", "Investigating", default);
        await service.Propose(Caller, r.Id, 4, "propose", Content, 0, default);
        Assert.Empty(s.Snapshot().Knowledge.Faults); Assert.Empty(s.Snapshot().Engineering.Events);
        await service.Approve(Caller, r.Id, 5, "approve", default);
        var original = s.Snapshot(); await service.Status(Caller, r.Id, 6, "reopen", "Reopened", default);
        var reopened = await service.Get(Caller, r.Id, default);
        Assert.Equal("Investigating", s.Snapshot().Incidents.Incidents[0].Status);
        Assert.Equal("Resolved", original.Engineering.Cases[0].Status); Assert.Equal("Reopened", reopened.Status);
        Assert.Single(reopened.Approvals); Assert.Equal("Synthetic engineering note", Assert.Single(reopened.Notes).Text);
        Assert.Equal(7, reopened.History.Length); Assert.NotNull(reopened.History.Single(h => h.Action == "proposed").Proposal);
        Assert.Single(s.Snapshot().Engineering.Events); Assert.Null(reopened.Proposal);
        await service.Status(Caller, r.Id, 7, "restart", "Investigating", default);
        await service.Propose(Caller, r.Id, 8, "propose-again", Content, 1, default);
        await service.Approve(Caller, r.Id, 9, "approve-again", default);
        Assert.Equal(2, s.Snapshot().Engineering.Events.Count); Assert.Equal(2, s.Snapshot().Knowledge.Faults[0].Publications.Length);
        Assert.Single(original.Knowledge.Faults[0].Publications);
    }
    [Fact]
    public async Task Approval_publishes_versioned_knowledge_incident_and_stable_event_once_under_concurrent_retry()
    {
        var s = await Setup(); var id = await Proposed(s); var service = Service(s);
        var result = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => service.Approve(Caller, id, 3, "approve", default)));
        Assert.All(result, r => Assert.Equal(result[0], r));
        var tx = s.Snapshot(); var fault = Assert.Single(tx.Knowledge.Faults); var publication = Assert.Single(fault.Publications);
        var approval = Assert.Single(tx.Engineering.Cases[0].Approvals); var e = Assert.Single(tx.Engineering.Events);
        Assert.Equal(Actor, publication.ApprovedBy); Assert.Equal(Content, publication.Content);
        Assert.Equal(fault.Id, approval.FaultId); Assert.Equal(2, approval.OntologyVersion);
        Assert.Equal(DocumentRules.Checksum(tx.Documents.Documents[0].Draft), Assert.Single(approval.Evidence).Checksum);
        Assert.Equal("Resolved", tx.Incidents.Incidents[0].Status); Assert.Null(tx.Incidents.Incidents[0].Knowledge); // intake immutable
        Assert.Equal("Pending", e.State); Assert.Equal(IncidentId, e.IncidentId); Assert.Equal(IncidentId, e.DriverId); Assert.Equal(e.Id, approval.EventId);
        Assert.Single(tx.Knowledge.Audit); Assert.Single(tx.Incidents.Audit);
        await s.Execute(Tenant, t => { t.Documents.Documents.Clear(); t.Ontology.Catalog = null; return 0; }, default);
        Assert.Equal(result[0], await service.Approve(Caller, id, 3, "approve", default));
        var first = await service.Events(Caller, id, 1, 20, default); var second = await service.Events(Caller, id, 1, 20, default);
        Assert.Equal(Assert.Single(first.Items).Id, Assert.Single(second.Items).Id); Assert.Single(s.Snapshot().Knowledge.Faults[0].Publications);
        var lookup = await new KnowledgeService(s, new RejectTaxonomy(), TimeProvider.System).Lookup(new(Tenant, Actor, Client, "lookup"), "SYN-EPS:001", "EPS", "SYN-MODEL-1", "1.0", default);
        Assert.Equal(fault.Id, lookup.Id); Assert.Equal(publication.Version, lookup.Version); Assert.Single(lookup.Content.Remedies);
    }
    private sealed class RejectTaxonomy : IFaultTaxonomy
    { public Task<bool> ValidateAsync(string tenantId, string system, FaultContent content, CancellationToken ct) => Task.FromResult(false); }
    [Theory]
    [InlineData("document", 404)]
    [InlineData("ontology", 400)]
    [InlineData("knowledge", 409)]
    [InlineData("incident", 409)]
    public async Task Changed_authoritative_context_prevents_partial_publication(string dependency, int status)
    {
        var s = await Setup(); var id = await Proposed(s);
        await s.Execute(Tenant, t =>
        {
            if (dependency == "document") t.Documents.Documents.Clear();
            if (dependency == "ontology") t.Ontology.Catalog = null;
            if (dependency == "knowledge") t.Knowledge.Faults.Add(new(IncidentId, Tenant, "SYN-EPS:001", "EPS", false, 1, Content, [], []));
            if (dependency == "incident") t.Incidents.Incidents[0] = t.Incidents.Incidents[0] with { Version = 3 };
            return 0;
        }, default);
        await Error(status, () => Service(s).Approve(Caller, id, 3, "approve", default));
        Assert.Equal("AwaitingApproval", s.Snapshot().Engineering.Cases[0].Status); Assert.Empty(s.Snapshot().Engineering.Events);
        Assert.Empty(s.Snapshot().Knowledge.Audit); Assert.Empty(s.Snapshot().Incidents.Audit);
    }
    [Fact]
    public async Task Commit_failure_and_cancellation_roll_back_all_six_states_and_allow_retry()
    {
        var s = await Setup(); var id = await Proposed(s); var before = JsonSerializer.Serialize(s.Snapshot());
        s.FailCommit = true; await Error(503, () => Service(s).Approve(Caller, id, 3, "approve", default));
        Assert.Equal(before, JsonSerializer.Serialize(s.Snapshot())); s.FailCommit = false;
        using var cancel = new CancellationTokenSource(); s.BeforeCommit = cancel.Cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(s).Approve(Caller, id, 3, "approve", cancel.Token));
        Assert.Equal(before, JsonSerializer.Serialize(s.Snapshot())); s.BeforeCommit = null;
        await Service(s).Approve(Caller, id, 3, "approve", default); Assert.Single(s.Snapshot().Engineering.Events);
    }
    [Fact]
    public async Task Concurrent_different_updates_have_one_winner_and_changed_replay_conflicts()
    {
        var s = await Setup(); var id = (await Create(s)).Id;
        async Task<int> Change(string key) { try { await Service(s).Note(Caller, id, 1, key, key, default); return 200; } catch (IncidentException e) { return e.Status; } }
        var result = await Task.WhenAll(Change("first"), Change("second")); Assert.Equal(new[] { 200, 409 }, result.Order().ToArray());
        var note = s.Snapshot().Engineering.Cases[0].Notes[0]; await Error(409, () => Service(s).Note(Caller, id, 1, note.Text, "changed", default));
        Assert.Single(s.Snapshot().Engineering.Cases[0].Notes);
    }
    [Theory]
    [InlineData("unapproved")]
    [InlineData("inapplicable")]
    [InlineData("classification")]
    [InlineData("reference")]
    public async Task Draft_and_inapplicable_resolution_evidence_cannot_publish(string kind)
    {
        var s = await Setup(); var id = (await Create(s)).Id; await Service(s).Status(Caller, id, 1, "start", "Investigating", default);
        var content = kind switch
        {
            "unapproved" => Content with { Remedies = [Content.Remedies[0] with { Approved = false }] },
            "inapplicable" => Content with { Remedies = [Content.Remedies[0] with { Model = "SYN-OTHER" }] },
            "classification" => Content with { ClassificationVersion = 1 },
            _ => Content with { References = [new("SYN-DOC", "2")] }
        };
        await Error(kind == "reference" ? 404 : 400, () => Service(s).Propose(Caller, id, 2, "proposal", content, 0, default));
        Assert.Empty(s.Snapshot().Knowledge.Faults); Assert.Empty(s.Snapshot().Engineering.Events);
    }
    [Fact]
    public async Task Tenant_owner_filters_precede_page_and_counts_and_replay_access()
    {
        var s = await Setup(); var id = (await Create(s)).Id; var service = Service(s);
        var own = Caller with { AllVehicles = false, OwnVehicles = true };
        Assert.Single((await service.List(own, 1, 20, null, default)).Items);
        var other = own with { ActorId = Client };
        Assert.Equal(0, (await service.List(other, 1, 20, null, default)).TotalCount);
        await Error(404, () => service.Get(other, id, default)); await Error(404, () => service.History(other, id, 1, 20, default));
        await Error(404, () => service.Events(other, id, 1, 20, default));
        await Error(404, () => service.Create(other, IncidentId, RunId, 2, "create", default));
        await Error(404, () => service.Get(Caller with { TenantId = Client }, id, default));
        await Error(403, () => service.Get(Caller with { ActorId = "malformed" }, id, default));
        Assert.Equal(1, (await service.List(Caller, int.MaxValue, 100, null, default)).TotalCount);
        Assert.Empty((await service.List(Caller, int.MaxValue, 100, null, default)).Items);
        await Error(400, () => service.List(Caller, 1, 101, null, default));
    }
    [Fact]
    public async Task Real_signed_API_enforces_separate_read_write_and_approval_permissions_and_safe_errors()
    {
        var s = await Setup(); using var keys = new Discovery();
        await using var factory = CreateFactory(keys).WithWebHostBuilder(b => b.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IEscalationStore>(s))));
        using var client = factory.CreateClient(); const string path = "/api/v1/engineering-escalations";
        void Auth(string? scopes, string[] roles, bool machine = false, string actor = Actor) =>
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthorizationTests.Token(keys.Key, scopes, roles, machine, actor));
        async Task<JsonElement> Send(int status, HttpMethod method, string suffix = "", object? body = null, string? version = "\"1\"", string? key = "api")
        {
            using var request = new HttpRequestMessage(method, path + suffix);
            if (method != HttpMethod.Get) request.Content = JsonContent.Create(body ?? new { });
            if (version is not null) request.Headers.TryAddWithoutValidation("If-Match", version);
            if (key is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
            using var response = await client.SendAsync(request); var raw = await response.Content.ReadAsStringAsync();
            Assert.True((int)response.StatusCode == status, raw);
            var json = JsonSerializer.Deserialize<JsonElement>(raw);
            if (status >= 400)
            {
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
                Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), json.GetProperty("correlationId").GetString());
                Assert.DoesNotContain("Exception", raw);
            }
            else if (method != HttpMethod.Get) Assert.NotNull(response.Headers.ETag);
            return json;
        }
        var create = new { incidentId = IncidentId, investigationId = RunId };
        await Send(401, HttpMethod.Get);
        Auth("escalations.read faults.read vehicles.read knowledge.read", ["Reader"]);
        await Send(200, HttpMethod.Get); await Send(403, HttpMethod.Post, body: create, version: "\"2\"");
        Auth("escalations.write faults.read vehicles.read knowledge.read", ["Operator"]); await Send(403, HttpMethod.Post, body: create);
        Auth("escalations.write faults.read vehicles.read knowledge.read", ["Administrator"]); await Send(403, HttpMethod.Post, body: create);
        Auth("escalations.write faults.read vehicles.read", ["Engineer"]); await Send(403, HttpMethod.Post, body: create);
        Auth("escalations.write faults.read vehicles.read knowledge.read", ["Engineer"]);
        foreach (var v in new string?[] { null, "1", "W/\"2\"", "\"02\"", "\"0\"", "\"2\",\"3\"" })
            await Send(400, HttpMethod.Post, body: create, version: v);
        foreach (var k in new string?[] { null, "bad key", "bad/key" }) await Send(400, HttpMethod.Post, body: create, key: k);
        await Send(400, HttpMethod.Post, body: new { incidentId = IncidentId, investigationId = RunId, approvedBy = Actor });
        var result = await Send(201, HttpMethod.Post, body: create, version: "\"2\""); var id = result.GetProperty("id").GetGuid();
        Assert.Equal(id, (await Send(201, HttpMethod.Post, body: create, version: "\"2\"")).GetProperty("id").GetGuid());
        await Send(403, HttpMethod.Get, "/" + id); // write does not imply escalation read
        await Send(200, HttpMethod.Put, $"/{id}/assignment", new { assigneeId = Actor });
        await Send(400, HttpMethod.Post, $"/{id}/notes", new { text = "x", instructions = "publish" }, "\"2\"");
        await Send(200, HttpMethod.Post, $"/{id}/notes", new { text = "Synthetic adjudication" }, "\"2\"");
        await Send(409, HttpMethod.Put, $"/{id}/status", new { status = "Resolved" }, "\"3\"");
        await Send(200, HttpMethod.Put, $"/{id}/status", new { status = "Investigating" }, "\"3\"");
        var content = JsonSerializer.SerializeToElement(Content, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await Send(200, HttpMethod.Post, $"/{id}/resolutions", new { content, expectedFaultVersion = 0 }, "\"4\"");
        await Send(403, HttpMethod.Post, $"/{id}/approvals", version: "\"5\"");
        Auth("knowledge.publish faults.read vehicles.read knowledge.read", ["Engineer"]);
        await Send(403, HttpMethod.Post, $"/{id}/approvals", version: "\"5\"");
        Auth("escalations.write knowledge.publish faults.read vehicles.read knowledge.read", ["Engineer"]);
        await Send(400, HttpMethod.Post, $"/{id}/approvals", new { approved = true }, "\"5\"");
        await Send(200, HttpMethod.Post, $"/{id}/approvals", version: "\"5\"");
        Auth("escalations.read faults.read vehicles.read.own knowledge.read", ["Reader"], actor: Client);
        await Send(404, HttpMethod.Get, "/" + id);
        Assert.Equal(0, (await Send(200, HttpMethod.Get)).GetProperty("totalCount").GetInt32());
        Auth("escalations.read faults.read vehicles.read.own knowledge.read", ["Reader"]);
        var detail = await Send(200, HttpMethod.Get, "/" + id); Assert.False(detail.TryGetProperty("tenantId", out _));
        Assert.Equal("Resolved", detail.GetProperty("status").GetString());
        await Send(200, HttpMethod.Get, $"/{id}/history"); var events = await Send(200, HttpMethod.Get, $"/{id}/events");
        Assert.Contains("revalidate", events.GetProperty("items")[0].GetProperty("dispatchPolicy").GetString());
        await Send(400, HttpMethod.Get, "?pageSize=101"); await Send(400, HttpMethod.Get, "?status=Notified");
        Auth(null, ["Escalations.Read.All", "Faults.Read.All", "Vehicles.Read.All", "Knowledge.Read.All"], true);
        await Send(200, HttpMethod.Get, "/" + id);
        Auth(null, ["Escalations.Write", "Faults.Read.All", "Vehicles.Read.All", "Knowledge.Read.All"], true);
        await Send(200, HttpMethod.Post, $"/{id}/notes", new { text = "Synthetic machine note" }, "\"6\"", "machine-note");
        await using var unavailable = CreateFactory(keys); using var prod = unavailable.CreateClient();
        prod.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthorizationTests.Token(keys.Key, "escalations.read faults.read vehicles.read knowledge.read", ["Reader"], false));
        var failure = await prod.GetAsync(path); Assert.Equal(503, (int)failure.StatusCode);
    }
    private sealed class UnknownProvider : IAgentProvider
    {
        public bool Enabled => true;
        public string Model => "synthetic-agent-2026-10-20";
        public Task<AgentCompletion> Complete(AgentInvocation invocation, CancellationToken ct) => Task.FromResult(new AgentCompletion(
            JsonSerializer.Serialize(new { incidentId = invocation.IncidentId, classification = "UnknownFault", citationIds = Array.Empty<Guid>(), reviewRequired = false }), Model, 100, 100, "stop", 0));
    }
    [Fact]
    public async Task Workflow_C_uses_real_investigation_then_engineering_and_approved_lookup_without_delivery()
    {
        var s = await Setup(); await s.Execute(Tenant, tx => { tx.Investigations.Results.Clear(); return 0; }, default);
        var runtime = new InvestigationService(s, new(s, TimeProvider.System), new(s, new ApprovedOntologyTaxonomy(s), TimeProvider.System),
            new(s, TimeProvider.System), new(s, new TestEmbedding(), new TestDocumentIndex(), TimeProvider.System), new UnknownProvider(), new(), TimeProvider.System);
        var result = await runtime.Run(Caller, IncidentId, 2, "real-investigation", default);
        Assert.Equal("UnknownFault", result.Classification); Assert.Equal("Pending", result.ProposedAction.Status);
        Assert.Empty(s.Snapshot().Engineering.Cases);
        var service = Service(s); var created = await service.Create(Caller, IncidentId, result.Id, 2, "case", default);
        await service.Status(Caller, created.Id, 1, "start", "Investigating", default);
        await service.Propose(Caller, created.Id, 2, "propose", Content, 0, default);
        Assert.Empty(s.Snapshot().Engineering.Events); Assert.Empty(s.Snapshot().Knowledge.Faults);
        await service.Approve(Caller, created.Id, 3, "approve", default);
        var approved = await new KnowledgeService(s, new ApprovedOntologyTaxonomy(s), TimeProvider.System).Lookup(new(Tenant, Actor, Client, "lookup"), "SYN-EPS:001", "EPS", "SYN-MODEL-1", "1.0", default);
        Assert.Equal(Content.Description, approved.Content.Description); Assert.Single(approved.Content.Remedies);
        Assert.Equal("Resolved", s.Snapshot().Incidents.Incidents[0].Status); Assert.Equal("Pending", Assert.Single(s.Snapshot().Engineering.Events).State);
        Assert.Equal("Pending", Assert.Single(s.Snapshot().Investigations.Results).Result.ProposedAction.Status);
    }
    [Fact]
    public async Task Evidence_publication_change_even_same_source_version_requires_a_new_proposal()
    {
        var s = await Setup(); var id = await Proposed(s);
        await s.Execute(Tenant, tx =>
        {
            var d = tx.Documents.Documents[0]; var changed = d.Draft with { Pages = [new(1, "Changed approved source")] };
            tx.Documents.Documents[0] = d with { Version = 3, Publications = d.Publications.Add(new(3, changed, DocumentRules.Checksum(changed), Actor, DateTimeOffset.UtcNow)) }; return 0;
        }, default);
        await Error(409, () => Service(s).Approve(Caller, id, 3, "approve", default));
        Assert.Empty(s.Snapshot().Knowledge.Faults); Assert.Empty(s.Snapshot().Engineering.Events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Approval_requires_each_original_grant_for_humans_and_machines(bool machine)
    {
        var s = await Setup(); var id = await Proposed(s); using var keys = new Discovery();
        await using var factory = CreateFactory(keys).WithWebHostBuilder(b => b.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton<IEscalationStore>(s))));
        using var client = factory.CreateClient();
        var scopes = new[] { "escalations.write", "knowledge.publish", "faults.read", "vehicles.read", "knowledge.read" };
        var roles = new[] { "Escalations.Write", "Knowledge.Publish", "Faults.Read.All", "Vehicles.Read.All", "Knowledge.Read.All" };
        async Task<int> Approve(int omit)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthorizationTests.Token(keys.Key,
                machine ? null : string.Join(" ", scopes.Where((_, n) => n != omit)),
                machine ? roles.Where((_, n) => n != omit).ToArray() : ["Engineer"], machine));
            using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/engineering-escalations/{id}/approvals") { Content = JsonContent.Create(new { }) };
            req.Headers.Add("If-Match", "\"3\""); req.Headers.Add("Idempotency-Key", "permission-approval");
            using var res = await client.SendAsync(req); return (int)res.StatusCode;
        }
        for (var omitted = 0; omitted < scopes.Length; omitted++)
        { Assert.Equal(403, await Approve(omitted)); Assert.Empty(s.Snapshot().Engineering.Events); Assert.Empty(s.Snapshot().Knowledge.Faults); }
        Assert.Equal(200, await Approve(-1)); Assert.Single(s.Snapshot().Engineering.Events);
    }
    [Theory]
    [InlineData("publisher")]
    [InlineData("tenant")]
    [InlineData("deleted")]
    public async Task Inaccessible_canonical_sources_do_not_publish_or_disclose_other_investigation_text(string reason)
    {
        var s = await Setup(); var id = (await Create(s)).Id;
        await s.Execute(Tenant, tx =>
        {
            var d = tx.Documents.Documents[0];
            if (reason == "deleted") d = d with { Deleted = true };
            if (reason == "tenant") d = d with { TenantId = Client };
            if (reason == "publisher") d = d with { Publications = [d.Publications[0] with { Content = d.Draft with { Access = "publisher" }, ApprovedBy = Client }] };
            tx.Documents.Documents[0] = d; return 0;
        }, default);
        await Service(s).Status(Caller, id, 1, "start", "Investigating", default);
        await Error(404, () => Service(s).Propose(Caller, id, 2, "propose", Content, 0, default));
        Assert.Empty(s.Snapshot().Knowledge.Faults); Assert.Empty(s.Snapshot().Engineering.Events);
    }
    [Fact]
    public async Task Withdraw_rejects_direct_approval_and_reopen_cannot_bypass_later_notification_state()
    {
        var s = await Setup(); var id = await Proposed(s); var service = Service(s);
        await Error(409, () => service.Status(Caller, id, 3, "bypass", "Resolved", default));
        await service.Status(Caller, id, 3, "withdraw", "Investigating", default);
        Assert.Null((await service.Get(Caller, id, default)).Proposal);
        await Error(409, () => service.Approve(Caller, id, 4, "approve", default));
        await service.Propose(Caller, id, 4, "new-proposal", Content, 0, default); await service.Approve(Caller, id, 5, "approve", default);
        await s.Execute(Tenant, tx => { tx.Incidents.Incidents[0] = tx.Incidents.Incidents[0] with { Status = "NotificationPending" }; return 0; }, default);
        await Error(409, () => service.Status(Caller, id, 6, "reopen", "Reopened", default));
        Assert.Equal("Resolved", s.Snapshot().Engineering.Cases[0].Status); Assert.Single(s.Snapshot().Engineering.Events);
    }

}
