using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FaultCode.Application.Escalations;
using FaultCode.Application.Notifications;
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
internal sealed class TestEngineeringStore : IEscalationStore, IKnowledgeStore, IIncidentStore, IInvestigationStore, IOntologyStore, IDocumentStore, INotificationStore, IFleetStore
{
    private readonly SemaphoreSlim gate = new(1);
    private readonly AsyncLocal<NotificationTransaction?> active = new();
    private readonly Dictionary<string, NotificationTransaction> tenants = [];
    public bool FailCommit { get; set; }
    public Action? BeforeCommit { get; set; }
    private static EngineeringTransaction Copy(EngineeringTransaction t) => new(t.Engineering.Copy(), t.Incidents.Copy(), t.Knowledge.Copy(), t.Ontology.Copy(), t.Documents.Copy(), t.Investigations.Copy());
    private static NotificationTransaction Copy(NotificationTransaction t) => new(t.Notifications.Copy(), t.Fleet.Copy(), Copy(t.Context));
    public NotificationTransaction NotificationSnapshot(string tenant = Tenant) => tenants.TryGetValue(tenant, out var tx) ? Copy(tx) : new(new(), new(), new(new(), new(), new(), new(), new(), new()));
    public EngineeringTransaction Snapshot(string tenant = Tenant) => NotificationSnapshot(tenant).Context;
    public Task<T> NotificationExecute<T>(string tenant, Func<NotificationTransaction, T> action, CancellationToken ct = default) =>
        ExecuteJoined(tenant, tx => Task.FromResult(action(tx)), ct);
    Task<T> INotificationStore.Execute<T>(string tenant, Func<NotificationTransaction, T> action, CancellationToken ct) => NotificationExecute(tenant, action, ct);
    public Task<T> Execute<T>(string tenant, Func<EngineeringTransaction, T> action, CancellationToken ct) =>
        ExecuteJoined(tenant, tx => Task.FromResult(action(tx.Context)), ct);
    private Task<T> ExecuteAsync<T>(string tenant, Func<EngineeringTransaction, Task<T>> action, CancellationToken ct) => ExecuteJoined(tenant, tx => action(tx.Context), ct);
    private async Task<T> ExecuteJoined<T>(string tenant, Func<NotificationTransaction, Task<T>> action, CancellationToken ct)
    {
        if (active.Value is { } joined) return await action(joined);
        await gate.WaitAsync(ct);
        try
        {
            var tx = NotificationSnapshot(tenant); active.Value = tx;
            var result = await action(tx); BeforeCommit?.Invoke(); ct.ThrowIfCancellationRequested();
            if (FailCommit) throw new IncidentException(503);
            tenants[tenant] = Copy(tx); return result;
        }
        finally { active.Value = null; gate.Release(); }
    }
    Task<FleetState> IFleetStore.ReadAsync(string tenant, CancellationToken ct) => NotificationExecute(tenant, tx => tx.Fleet.Copy(), ct);
    Task<T> IFleetStore.TransactAsync<T>(string tenant, Func<FleetState, T> action, CancellationToken ct) => NotificationExecute(tenant, tx => action(tx.Fleet), ct);
    Task<IncidentState> IIncidentStore.ReadAsync(string tenant, CancellationToken ct) => Execute(tenant, tx => tx.Incidents.Copy(), ct);
    Task<T> IIncidentStore.TransactAsync<T>(string tenant, Func<IncidentTransaction, T> action, CancellationToken ct) =>
        NotificationExecute(tenant, tx => action(new(tx.Context.Incidents, () => new(tx.Fleet.Vehicles, tx.Fleet.Drivers, tx.Fleet.Associations, tx.Context.Knowledge.Faults))), ct);
    Task<OntologyState> IOntologyStore.ReadAsync(string tenant, CancellationToken ct) => Execute(tenant, tx => tx.Ontology.Copy(), ct);
    Task<T> IOntologyStore.TransactAsync<T>(string tenant, Func<OntologyState, T> action, CancellationToken ct) => Execute(tenant, tx => action(tx.Ontology), ct);
    Task<T> IInvestigationStore.Execute<T>(string tenant, Func<InvestigationState, Task<T>> action, CancellationToken ct) => ExecuteAsync(tenant, tx => action(tx.Investigations), ct);
    Task<T> IDocumentStore.Execute<T>(string tenant, Func<DocumentState, Task<T>> action, CancellationToken ct) => ExecuteAsync(tenant, tx => action(tx.Documents), ct);
    public Task<KnowledgeState> ReadAsync(string tenantId, CancellationToken ct) => Execute(tenantId, t => t.Knowledge.Copy(), ct);
    public Task<T> TransactAsync<T>(string tenantId, Func<KnowledgeState, T> operation, CancellationToken ct) => Execute(tenantId, t => operation(t.Knowledge), ct);
}
public sealed class EngineeringTests
{
    internal static readonly Guid IncidentId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    internal static readonly Guid RunId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    internal static readonly FleetCaller Caller = new(Tenant, Actor, Client, "demo-engineering", true, false, true, false);
    internal static readonly FaultContent Content = new("Demo adjudicated EPS finding", "SYN-CLASS", 2, ["SYN-SYM"], [new("SYN-DOC", "1")],
        [new("SYN-REMEDY", "guidance", "Demo engineering approved inspection guidance.", true, "SYN-MODEL-1", "1.0", null)]);
    private static EscalationService Service(TestEngineeringStore store) => new(store, TimeProvider.System);
    internal static async Task<TestEngineeringStore> Setup(string classification = "UnknownFault")
    {
        var s = new TestEngineeringStore(); var now = DateTimeOffset.UtcNow;
        await s.Execute(Tenant, tx =>
        {
            var i = new Incident(IncidentId, Tenant, Client, "SYN-EVENT", "SYN-EPS:001", "EPS", "SYN-MODEL-1", "1.0", now, now, "intake",
                new(IncidentId, "SYN-VEHICLE", 1, IncidentId, "SYN-DRIVER", 1, Actor, new(IncidentId, IncidentId, IncidentId, now, null)),
                null, "Investigating", 2, [], [new(IncidentId, "investigate", "Pending", 0, null)]);
            tx.Incidents.Incidents.Add(i);
            tx.Investigations.Results.Add(new(Tenant, Actor, Actor, Client, new(RunId, i.Id, 1, now, "demo", classification, null, null,
                [], null, ["no-approved-definition"], new("engineering-escalation", "Pending"), new("demo", "investigation-v1", 1, 100, 100, 6500, false), "investigation-v1")));
            var ontology = new OntologyContent([new("SYN-CLASS", "classification", "EPS", "Demo class", ["guidance", "dealership"])], [], []);
            tx.Ontology.Catalog = new(IncidentId, 2, ontology, [], [new(2, ontology, Actor, now)]);
            var doc = new DocumentContent("Demo source", "1", "EPS", "SYN-MODEL-1", "1.0", "tenant", [new(1, "Demo canonical engineering source.")]);
            tx.Documents.Documents.Add(new(IncidentId, Tenant, "SYN-DOC", 2, false, doc, [], [new(2, doc, DocumentRules.Checksum(doc), Actor, now)], null));
            return 0;
        }, default); return s;
    }
    private static Task<MutationResult> Create(TestEngineeringStore s, string key = "create") => Service(s).Create(Caller, IncidentId, RunId, 2, key, default);
    internal static async Task<Guid> Proposed(TestEngineeringStore s, long expectedFaultVersion = 0)
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
        await service.Note(Caller, r.Id, 2, "note", "Demo engineering note", default);
        Assert.Equal(3, (await service.Get(Caller, r.Id, default)).Version);
        await service.Status(Caller, r.Id, 3, "start", "Investigating", default);
        await service.Propose(Caller, r.Id, 4, "propose", Content, 0, default);
        Assert.Empty(s.Snapshot().Knowledge.Faults); Assert.Empty(s.Snapshot().Engineering.Events);
        await service.Approve(Caller, r.Id, 5, "approve", default);
        var original = s.Snapshot(); await service.Status(Caller, r.Id, 6, "reopen", "Reopened", default);
        var reopened = await service.Get(Caller, r.Id, default);
        Assert.Equal("Investigating", s.Snapshot().Incidents.Incidents[0].Status);
        Assert.Equal("Resolved", original.Engineering.Cases[0].Status); Assert.Equal("Reopened", reopened.Status);
        Assert.Single(reopened.Approvals); Assert.Equal("Demo engineering note", Assert.Single(reopened.Notes).Text);
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
    private sealed class UnknownProvider : IAgentProvider
    {
        public bool Enabled => true;
        public string Model => "demo-agent-2026-10-20";
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
