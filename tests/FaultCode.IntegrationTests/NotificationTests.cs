using FaultCode.Application.Telemetry;
using FaultCode.Infrastructure.Telemetry;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FaultCode.Application.Escalations;
using FaultCode.Application.Notifications;
using FaultCode.Application.Fleet;
using FaultCode.Application.Incidents;
using FaultCode.Application.Investigations;
using FaultCode.Application.Knowledge;
using FaultCode.Application.Ontology;
using FaultCode.Application.Retrieval;
using FaultCode.Application.Agents;
using FaultCode.Domain;
using FaultCode.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static FaultCode.IntegrationTests.EntraAuthenticationTests;
using static FaultCode.IntegrationTests.EngineeringTests;

namespace FaultCode.IntegrationTests;

public sealed class NotificationTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance(int seconds = 60) => Now = Now.AddSeconds(seconds);
    }
    private sealed class Sink : INotificationDelivery
    {
        public LocalNotificationSink Local { get; } = new();
        public int Calls { get; private set; }
        public string? Failure { get; set; }
        public Action? BeforeAck { get; set; }
        public TaskCompletionSource? Waiting { get; set; }
        public TaskCompletionSource? Release { get; set; }
        public async Task<DeliveryAcknowledgement> Send(LocalDelivery message, CancellationToken ct)
        {
            Calls++; Waiting?.TrySetResult();
            if (Release is not null) await Release.Task.WaitAsync(ct);
            if (Failure == "timeout") throw new OperationCanceledException();
            if (Failure == "failure") throw new Exception("secret-provider-body");
            if (Failure == "invalid") return new("");
            var ack = await Local.Send(message, ct); BeforeAck?.Invoke(); return ack;
        }
    }
    private static NotificationService Service(TestEngineeringStore s, Sink? sink = null, Clock? clock = null) => new(s, sink ?? new(), clock ?? new());
    private static async Task Fleet(TestEngineeringStore s)
    {
        await s.NotificationExecute(Tenant, tx =>
        {
            var i = tx.Context.Incidents.Incidents[0];
            tx.Fleet.Drivers.Add(new(i.Fleet.DriverId, Tenant, "SYN-DRIVER", Actor, "Demo driver", "demo@example.test", true, true, 1));
            tx.Fleet.Vehicles.Add(new(i.Fleet.VehicleId, Tenant, "SYN-VEHICLE", "EPS", true, 1));
            tx.Fleet.Associations.Add(i.Fleet.Association); return 0;
        });
        await Service(s).Preferences(Caller, IncidentId, 1, "preferences", true, "SYN-CONTACT", default);
    }
    private static async Task<(TestEngineeringStore Store, Guid Source)> Engineering(ITelemetry? telemetry = null)
    {
        var s = await Setup(); await Fleet(s);
        await s.Execute(Tenant, tx => { tx.Investigations.Results.Clear(); return 0; }, default);
        var runtime = new InvestigationService(s, new(s, TimeProvider.System), new(s, new ApprovedOntologyTaxonomy(s), TimeProvider.System), new(s, TimeProvider.System),
            new(s, new TestEmbedding(), new TestDocumentIndex(), TimeProvider.System), new EchoProvider(), new(), TimeProvider.System);
        var result = await runtime.Run(Caller, IncidentId, 2, "unknown-investigation", default);
        Assert.Equal("UnknownFault", result.Classification);
        var engineering = new EscalationService(s, TimeProvider.System, telemetry);
        var created = await engineering.Create(Caller, IncidentId, result.Id, 2, "create", default);
        await engineering.Status(Caller, created.Id, 1, "start", "Investigating", default);
        await engineering.Propose(Caller, created.Id, 2, "propose", Content, 0, default);
        await engineering.Approve(Caller, created.Id, 3, "approve", default);
        return (s, s.Snapshot().Engineering.Events[0].Id);
    }
    private static async Task<(TestEngineeringStore Store, Guid Source)> Known(bool patch = false)
    {
        var s = await Setup(); await Fleet(s);
        await s.Execute(Tenant, tx =>
        {
            var i = tx.Incidents.Incidents[0];
            var remedy = patch ? Content.Remedies[0] with { Kind = "dealership", Patch = new("SYN-PATCH-1", "1.0") } : Content.Remedies[0];
            var content = Content with { Remedies = [remedy] };
            tx.Knowledge.Faults.Add(new(IncidentId, Tenant, i.Code, i.System, false, 2, content, [], [new(2, content, Actor, DateTimeOffset.UtcNow)]));
            var d = tx.Documents.Documents[0]; var dc = d.Draft with { Pages = [new(1, remedy.Description)] };
            var publication = new DocumentPublication(2, dc, DocumentRules.Checksum(dc), Actor, DateTimeOffset.UtcNow);
            tx.Documents.Documents[0] = d = d with { Draft = dc, Publications = [publication] };
            var chunk = DocumentRules.Chunks(d, publication)[0];
            var ontology = tx.Ontology.Catalog!;
            var rule = new InvestigationRule(2, Actor, DateTimeOffset.UtcNow, new(2, "EPS", ["SYN-SYM"], "SYN-MODEL-1", "1.0"),
                new("SYN-RULE", "SYN-CLASS", "SYN-SEV", [remedy.Kind], ["SYN-RULE"], "Demo rule"));
            var r = tx.Investigations.Results[0];
            tx.Investigations.Results[0] = r with
            {
                Result = r.Result with
                {
                    IncidentVersion = 2,
                    Classification = patch ? "KnownFaultWithApprovedPatch" : "KnownFault",
                    Definition = new(IncidentId, i.Code, i.System, 2, content, Actor, DateTimeOffset.UtcNow),
                    Rule = rule,
                    Remedy = remedy,
                    Citations = [new(chunk.Id, d.Id, d.SourceId, "1", 2, chunk.Page, chunk.Offset, chunk.Checksum, publication.Checksum, chunk.Text, .99, Actor, DateTimeOffset.UtcNow)],
                    ProposedAction = new(patch ? "dealership-referral" : "notification", "Pending")
                }
            };
            return 0;
        }, default); return (s, RunId);
    }
    private static Task<MutationResult> Queue(TestEngineeringStore s, Guid source, string kind = "engineering", string key = "enqueue") =>
        Service(s).Enqueue(Caller, IncidentId, kind, source, s.Snapshot().Incidents.Incidents[0].Version, key, default);
    private static async Task Error(int status, Func<Task> action) => Assert.Equal(status, (await Assert.ThrowsAsync<IncidentException>(action)).Status);

    [Fact]
    public async Task All_driver_list_filters_by_driver_and_respects_incident_visibility()
    {
        var (store, source) = await Engineering();
        var queued = await Queue(store, source);
        var service = Service(store);
        var all = await service.ListAll(Caller, null, 1, 20, default);
        Assert.Equal(queued.Id, Assert.Single(all.Items).Notification.Id);
        Assert.Equal("Demo driver", all.Items[0].DriverName);
        Assert.Equal("demo@example.test", all.Items[0].Email);
        Assert.Single((await service.ListAll(Caller, null, 1, 20, default, IncidentId)).Items);
        await Error(400, () => service.ListAll(Caller, null, 1, 20, default, Guid.Empty));
        await Error(404, () => service.ListAll(Caller, null, 1, 20, default, Guid.NewGuid()));
        var driverId = store.Snapshot().Incidents.Incidents[0].Fleet.DriverId;
        Assert.Single((await service.ListAll(Caller, driverId, 1, 20, default)).Items);
        Assert.Empty((await service.ListAll(Caller with { AllVehicles = false, OwnVehicles = false }, null, 1, 20, default)).Items);
        await Error(400, () => service.ListAll(Caller, Guid.Empty, 1, 20, default));
        await Error(404, () => service.ListAll(Caller, Guid.NewGuid(), 1, 20, default));
        await Error(400, () => service.ListAll(Caller, null, 0, 20, default));
    }

    [Fact]
    public async Task Handled_delivery_failure_exports_actionable_failure_without_contact_or_exception()
    {
        var (store, source) = await Engineering(); await using var receiver = await OtlpTelemetryTests.Receiver.Start();
        using var adapter = new OtlpTelemetryAdapter(new(receiver.Endpoint, 1)); var telemetry = new SafeTelemetry(adapter);
        var sink = new Sink { Failure = "failure" }; var service = new NotificationService(store, sink, TimeProvider.System, telemetry);
        var version = store.Snapshot().Incidents.Incidents[0].Version;
        var queued = await service.Enqueue(Caller, IncidentId, "engineering", source, version, "otlp-queue", default);
        var completed = await service.Dispatch(Caller, queued.Id, 1, "otlp-send", default);
        Assert.Equal("Failed", (await service.Get(Caller, completed.Id, default)).State); Assert.Equal(1, sink.Calls);
        Assert.True(adapter.ForceFlush());
        var attempt = Assert.Single(receiver.Items("traces"), x => x.Text(5) == "Delivery");
        Assert.Equal("Failed", attempt.Attributes(9)["faultcode.outcome"].Text(1)); Assert.Equal("Dependency", attempt.Attributes(9)["error.type"].Text(1));
        foreach (var message in receiver.Messages)
            foreach (var secret in new[] { "secret-provider-body", "SYN-CONTACT", Tenant, Actor, Client })
                Assert.DoesNotContain(secret, System.Text.Encoding.UTF8.GetString(message.Body));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_engineering_notification_commit_and_replay_survive_OTLP_outage(bool outage)
    {
        await using var receiver = await OtlpTelemetryTests.Receiver.Start(); receiver.Fail = outage;
        using var adapter = new OtlpTelemetryAdapter(new(receiver.Endpoint, 1)); var telemetry = new SafeTelemetry(adapter);
        using (var root = telemetry.Begin(TelemetryOperation.Api))
        {
            var (store, source) = await Engineering(telemetry); var sink = new Sink();
            var service = new NotificationService(store, sink, TimeProvider.System, telemetry);
            var version = store.Snapshot().Incidents.Incidents[0].Version;
            var queued = await service.Enqueue(Caller, IncidentId, "engineering", source, version, "otlp-queue", default);
            var sent = await service.Dispatch(Caller, queued.Id, 1, "otlp-send", default);
            Assert.Equal(sent, await service.Dispatch(Caller, queued.Id, 1, "otlp-send", default)); Assert.Equal(1, sink.Calls);
            await Error(403, () => service.Get(Caller with { ActorId = "invalid" }, queued.Id, default));
            root.Complete(TelemetryOutcome.Success);
        }
        adapter.ForceFlush(); var spans = receiver.Items("traces");
        Assert.Equal(4, spans.Count(x => x.Text(5) == "Engineering"));
        Assert.Equal(4, spans.Count(x => x.Text(5) == "Notification"));
        Assert.Single(spans.Select(x => x.Hex(1)).Distinct());
        foreach (var message in receiver.Messages)
            foreach (var secret in new[] { Tenant, Actor, Client, "SYN-CONTACT", "demo@example.test", "secret-provider-body" })
                Assert.DoesNotContain(secret, System.Text.Encoding.UTF8.GetString(message.Body));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Approved_A_B_templates_send_to_exact_driver_and_acknowledge(bool patch)
    {
        var (s, source) = await Known(patch); var original = JsonSerializer.Serialize(s.Snapshot().Investigations);
        var result = await Queue(s, source, "investigation"); var sink = new Sink(); var service = Service(s, sink);
        var pending = await service.Get(Caller, result.Id, default);
        Assert.Equal("Pending", pending.State); Assert.Empty(sink.Local.Accepted);
        Assert.Equal(Content.Remedies[0].Description, pending.Remedy.Description);
        Assert.Contains(IncidentId.ToString(), pending.Message); Assert.Equal(IncidentId, pending.DriverId);
        if (patch) { Assert.Contains("SYN-PATCH-1", pending.Message); Assert.Contains("dealership", pending.Message); Assert.Contains("no vehicle software has been installed", pending.Message); }
        else Assert.DoesNotContain("patch", pending.Message);
        var sent = await service.Dispatch(Caller, result.Id, 1, "dispatch", default);
        Assert.Equal(3, sent.Version); var n = await service.Get(Caller, result.Id, default);
        Assert.Equal("Sent", n.State); Assert.Equal("Notified", s.Snapshot().Incidents.Incidents[0].Status);
        Assert.NotNull(Assert.Single(n.Attempts).Acknowledgement); Assert.Equal("SYN-CONTACT", Assert.Single(sink.Local.Accepted).DemoContact);
        Assert.Equal(sent, await service.Dispatch(Caller, result.Id, 1, "dispatch", default)); Assert.Equal(1, sink.Calls);
        Assert.Equal(original, JsonSerializer.Serialize(s.Snapshot().Investigations));
    }
    [Fact]
    public async Task Real_engineering_C_deduplicates_event_and_retains_approved_history()
    {
        var (s, source) = await Engineering(); var version = s.Snapshot().Incidents.Incidents[0].Version;
        var requests = await Task.WhenAll(Enumerable.Range(0, 6).Select(n => Service(s).Enqueue(Caller, IncidentId, "engineering", source, version, "key-" + n, default)));
        Assert.Single(requests.Distinct()); Assert.Single(s.NotificationSnapshot().Notifications.Messages);
        var sink = new Sink(); var service = Service(s, sink); await service.Dispatch(Caller, requests[0].Id, 1, "send", default);
        var n = await service.Get(Caller, requests[0].Id, default); Assert.Equal(1, n.PublicationVersion); Assert.Equal(2, n.OntologyVersion);
        Assert.Equal("Pending", s.Snapshot().Engineering.Events[0].State); Assert.Equal(source, n.SourceId);
        Assert.Equal(3, (await service.History(Caller, n.Id, 1, 20, default)).TotalCount);
        Assert.Equal(n.Id, Assert.Single(sink.Local.Accepted).NotificationId); Assert.Equal(IncidentId, n.DriverId);
    }
    [Theory]
    [InlineData("disabled")]
    [InlineData("missing")]
    [InlineData("deactivated")]
    [InlineData("owner")]
    [InlineData("association")]
    [InlineData("vehicle")]
    public async Task Invalid_recipient_or_preferences_block_enqueue_and_dispatch(string reason)
    {
        var (s, source) = await Engineering(); var queued = await Queue(s, source); var sink = new Sink();
        await s.NotificationExecute(Tenant, tx =>
        {
            var d = tx.Fleet.Drivers[0];
            if (reason == "disabled") tx.Notifications.Preferences[0] = tx.Notifications.Preferences[0] with { Enabled = false };
            if (reason == "missing") tx.Notifications.Preferences.Clear();
            if (reason == "deactivated") tx.Fleet.Drivers[0] = d with { Active = false };
            if (reason == "owner") tx.Fleet.Drivers[0] = d with { OwnerObjectId = Client };
            if (reason == "association") tx.Fleet.Associations[0] = tx.Fleet.Associations[0] with { Until = DateTimeOffset.UtcNow };
            if (reason == "vehicle") tx.Fleet.Vehicles[0] = tx.Fleet.Vehicles[0] with { Active = false };
            return 0;
        });
        await Error(409, () => Service(s, sink).Dispatch(Caller, queued.Id, 1, "dispatch", default));
        Assert.Equal(0, sink.Calls); Assert.Empty(s.NotificationSnapshot().Notifications.Messages[0].Attempts);
    }
    [Theory]
    [InlineData("fault")]
    [InlineData("draft")]
    [InlineData("document")]
    [InlineData("publisher")]
    [InlineData("ontology")]
    [InlineData("case")]
    [InlineData("event")]
    [InlineData("incident")]
    public async Task Changed_current_approval_or_evidence_blocks_dispatch(string reason)
    {
        var (s, source) = await Engineering(); var n = await Queue(s, source); var sink = new Sink();
        await s.Execute(Tenant, tx =>
        {
            var f = tx.Knowledge.Faults[0]; var d = tx.Documents.Documents[0];
            if (reason == "fault") tx.Knowledge.Faults[0] = f with { Retired = true };
            if (reason == "draft") tx.Knowledge.Faults[0] = f with { Publications = [] };
            if (reason == "document") tx.Documents.Documents[0] = d with { Deleted = true };
            if (reason == "publisher") tx.Documents.Documents[0] = d with { Publications = [d.Publications[0] with { Content = d.Draft with { Access = "publisher" } }] };
            if (reason == "ontology") tx.Ontology.Catalog = null;
            if (reason == "case") tx.Engineering.Cases[0] = tx.Engineering.Cases[0] with { Status = "Reopened" };
            if (reason == "event") tx.Engineering.Events[0] = tx.Engineering.Events[0] with { ApprovalId = Guid.NewGuid() };
            if (reason == "incident") tx.Incidents.Incidents[0] = tx.Incidents.Incidents[0] with { Status = "Investigating" };
            return 0;
        }, default);
        await Error(409, () => Service(s, sink).Dispatch(Caller, n.Id, 1, "dispatch", default)); Assert.Equal(0, sink.Calls);
        Assert.Equal(n, await Service(s).Enqueue(Caller, IncidentId, "engineering", source, 3, "enqueue", default)); // exact replay before evidence
    }
    [Theory]
    [InlineData("failure", "delivery-unavailable")]
    [InlineData("timeout", "acceptance-unknown")]
    [InlineData("invalid", "invalid-acknowledgement")]
    public async Task Failure_backoff_retry_and_ack_history_are_honest(string failure, string code)
    {
        var (s, source) = await Engineering(); var n = await Queue(s, source); var sink = new Sink { Failure = failure }; var clock = new Clock(); var service = Service(s, sink, clock);
        var failed = await service.Dispatch(Caller, n.Id, 1, "first", default);
        var detail = await service.Get(Caller, n.Id, default); Assert.Equal("Failed", detail.State); Assert.Null(detail.Attempts[0].Acknowledgement);
        Assert.Equal(code, detail.Attempts[0].FailureCode); Assert.Equal("NotificationPending", s.Snapshot().Incidents.Incidents[0].Status);
        await Error(409, () => service.Dispatch(Caller, n.Id, failed.Version, "early", default)); Assert.Equal(1, sink.Calls);
        clock.Advance(); sink.Failure = null; var sent = await service.Dispatch(Caller, n.Id, failed.Version, "second", default);
        Assert.Equal("Sent", (await service.Get(Caller, n.Id, default)).State); Assert.Equal(2, s.NotificationSnapshot().Notifications.Messages[0].Attempts.Length);
        Assert.Single(sink.Local.Accepted); Assert.Equal(sent, await service.Dispatch(Caller, n.Id, failed.Version, "second", default));
    }
    [Fact]
    public async Task Concurrent_claims_and_cancellation_are_fenced()
    {
        var (s, source) = await Engineering(); var n = await Queue(s, source); var sink = new Sink { Waiting = new(), Release = new() }; var service = Service(s, sink);
        var running = service.Dispatch(Caller, n.Id, 1, "first", default); await sink.Waiting.Task;
        await Error(409, () => service.Dispatch(Caller, n.Id, 2, "other", default)); await Error(409, () => service.Cancel(Caller, n.Id, 2, "cancel", default));
        Assert.Equal("Pending", (await service.Get(Caller, n.Id, default)).State); sink.Release.SetResult(); await running; Assert.Equal(1, sink.Calls);
    }
    [Fact]
    public async Task Failed_ack_commit_recovers_lease_and_reuses_provider_key_without_exactly_once_claim()
    {
        var (s, source) = await Engineering(); var n = await Queue(s, source); var clock = new Clock(); var sink = new Sink { BeforeAck = () => s.FailCommit = true }; var service = Service(s, sink, clock);
        await Error(503, () => service.Dispatch(Caller, n.Id, 1, "first", default)); s.FailCommit = false; sink.BeforeAck = null;
        Assert.Single(sink.Local.Accepted); Assert.Equal("Pending", s.NotificationSnapshot().Notifications.Messages[0].State);
        clock.Advance(); var recovered = await service.Dispatch(Caller, n.Id, 2, "recover", default);
        Assert.Equal("acceptance-unknown", s.NotificationSnapshot().Notifications.Messages[0].Attempts[0].FailureCode);
        clock.Advance(); await service.Dispatch(Caller, n.Id, recovered.Version, "retry", default);
        Assert.Single(sink.Local.Accepted); Assert.Equal(2, sink.Calls); Assert.Equal("Sent", s.NotificationSnapshot().Notifications.Messages[0].State);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancel_or_supersede_coordinates_reopen_and_old_event_cannot_redeliver(bool sent)
    {
        var (s, source) = await Engineering(); var n = await Queue(s, source); var service = Service(s); long version = 1;
        if (sent) version = (await service.Dispatch(Caller, n.Id, 1, "dispatch", default)).Version;
        await service.Cancel(Caller, n.Id, version, "cancel", default);
        var old = s.NotificationSnapshot().Notifications.Messages[0]; Assert.Equal(sent ? "Superseded" : "Cancelled", old.State);
        Assert.Equal("Resolved", s.Snapshot().Incidents.Incidents[0].Status);
        var e = s.Snapshot().Engineering.Cases[0]; await new EscalationService(s, TimeProvider.System).Status(Caller, e.Id, e.Version, "reopen", "Reopened", default);
        var dedup = await Queue(s, source, key: "old-event"); Assert.Equal(n.Id, dedup.Id);
        await Error(409, () => service.Dispatch(Caller, n.Id, old.Version, "resend", default));
        Assert.Single(s.Snapshot().Engineering.Events); Assert.Single(s.NotificationSnapshot().Notifications.Messages);
    }
    [Fact]
    public async Task Failed_enqueue_and_preference_commit_and_cancellation_leave_all_states_unchanged()
    {
        var (s, source) = await Engineering(); var before = JsonSerializer.Serialize(s.NotificationSnapshot());
        s.FailCommit = true; await Error(503, () => Queue(s, source)); Assert.Equal(before, JsonSerializer.Serialize(s.NotificationSnapshot()));
        await Error(503, () => Service(s).Preferences(Caller, IncidentId, 2, "changed-pref", false, null, default));
        Assert.Equal(before, JsonSerializer.Serialize(s.NotificationSnapshot())); s.FailCommit = false;
        using var cancel = new CancellationTokenSource(); s.BeforeCommit = cancel.Cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(s).Enqueue(Caller, IncidentId, "engineering", source, 3, "enqueue", cancel.Token));
        Assert.Equal(before, JsonSerializer.Serialize(s.NotificationSnapshot())); s.BeforeCommit = null; await Queue(s, source);
    }
    [Fact]
    public async Task Enqueue_replay_payload_binding_visibility_and_source_approval_are_enforced()
    {
        var (s, source) = await Known(); var n = await Queue(s, source, "investigation");
        await Error(409, () => Service(s).Enqueue(Caller, IncidentId, "investigation", source, 1, "enqueue", default));
        await Error(409, () => Service(s).Enqueue(Caller, IncidentId, "engineering", source, 2, "enqueue", default));
        await Error(404, () => Service(s).Get(Caller with { TenantId = Client }, n.Id, default));
        await Error(404, () => Service(s).Get(Caller with { AllVehicles = false, OwnVehicles = true, ActorId = Client }, n.Id, default));
        await Error(404, () => Service(s).List(Caller with { AllDrivers = false, OwnDrivers = true, ActorId = Client }, IncidentId, 1, 20, default));
        var own = Caller with { AllVehicles = false, OwnVehicles = true, AllDrivers = false, OwnDrivers = true };
        Assert.Single((await Service(s).List(own, IncidentId, 1, 20, default)).Items);
        await Error(403, () => Service(s).Get(Caller with { ActorId = "malformed" }, n.Id, default));
        var (other, otherSource) = await Known();
        await Error(404, () => Service(other).Enqueue(Caller with { ActorId = Client }, IncidentId, "investigation", otherSource, 2, "queue", default));
        await other.Execute(Tenant, tx => { var e = tx.Investigations.Results[0]; tx.Investigations.Results[0] = e with { Result = e.Result with { Classification = "InsufficientEvidence" } }; return 0; }, default);
        await Error(409, () => Queue(other, otherSource, "investigation")); Assert.Empty(other.NotificationSnapshot().Notifications.Messages);
    }
    [Fact]
    public async Task Local_preferences_default_disabled_and_email_flags_are_not_consent()
    {
        var (s, source) = await Engineering(); await s.NotificationExecute(Tenant, tx => { tx.Notifications.Preferences.Clear(); return 0; });
        Assert.False((await Service(s).Preference(Caller, IncidentId, default)).Enabled);
        await Error(409, () => Queue(s, source)); Assert.Empty(s.NotificationSnapshot().Notifications.Messages);
    }
    [Fact]
    public async Task Maximum_attempts_and_changed_preferences_cannot_silently_redirect()
    {
        var (s, source) = await Engineering(); var n = await Queue(s, source); var clock = new Clock(); var sink = new Sink { Failure = "failure" }; var service = Service(s, sink, clock); long version = 1;
        for (var at = 0; at < 5; at++) { version = (await service.Dispatch(Caller, n.Id, version, "attempt-" + at, default)).Version; clock.Advance(600); }
        await Error(409, () => service.Dispatch(Caller, n.Id, version, "exhausted", default)); Assert.Equal(5, sink.Calls);
        var (changed, changedSource) = await Engineering(); var queued = await Queue(changed, changedSource); var noSend = new Sink();
        await Service(changed).Preferences(Caller, IncidentId, 2, "redirect", true, "SYN-OTHER", default);
        await Error(409, () => Service(changed, noSend).Dispatch(Caller, queued.Id, 1, "send", default)); Assert.Equal(0, noSend.Calls);
    }
    private sealed class EchoProvider : IAgentProvider
    {
        public bool Enabled => true;
        public string Model => "demo-agent-2026-10-20";
        public Task<AgentCompletion> Complete(AgentInvocation invocation, CancellationToken ct)
        {
            var input = JsonSerializer.Deserialize<JsonElement>(invocation.Input);
            return Task.FromResult(new AgentCompletion(JsonSerializer.Serialize(new
            {
                incidentId = input.GetProperty("incidentId"),
                classification = input.GetProperty("classification"),
                citationIds = input.GetProperty("citationIds"),
                reviewRequired = false
            }), Model, 100, 100, "stop", 0));
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_investigation_A_B_to_approved_notification_and_local_acknowledgement(bool patch)
    {
        var (s, _) = await Known(patch); var index = new TestDocumentIndex(); var embedding = new TestEmbedding();
        await s.Execute(Tenant, tx =>
        {
            tx.Investigations.Results.Clear(); var catalog = tx.Ontology.Catalog!;
            var content = new OntologyContent([new("SYN-CLASS", "classification", "EPS", "Demo class", ["guidance", "dealership"]),
                new("SYN-SYM", "symptom", "EPS", "Demo symptom", []), new("SYN-SEV", "severity", "EPS", "Demo severity", [])], [],
                [new("SYN-RULE", "EPS", ["SYN-SYM"], "SYN-CLASS", "SYN-SEV", ["guidance", "dealership"], 1, null, null)]);
            tx.Ontology.Catalog = catalog with { Draft = content, Publications = [new(2, content, Actor, DateTimeOffset.UtcNow)] }; return 0;
        }, default);
        var retrieval = new RetrievalService(s, embedding, index, TimeProvider.System);
        await retrieval.Index(new(Tenant, Actor, Client, "index"), IncidentId, 2, "index", default);
        var investigate = new InvestigationService(s, new(s, TimeProvider.System), new(s, new ApprovedOntologyTaxonomy(s), TimeProvider.System), new(s, TimeProvider.System), retrieval, new EchoProvider(), new(), TimeProvider.System);
        var result = await investigate.Run(Caller, IncidentId, 2, "investigate", default);
        Assert.Equal(patch ? "KnownFaultWithApprovedPatch" : "KnownFault", result.Classification);
        var n = await Queue(s, result.Id, "investigation"); var sink = new Sink(); await Service(s, sink).Dispatch(Caller, n.Id, 1, "send", default);
        Assert.Equal("Notified", s.Snapshot().Incidents.Incidents[0].Status); Assert.Single(sink.Local.Accepted);
        Assert.Equal("Pending", Assert.Single(s.Snapshot().Investigations.Results).Result.ProposedAction.Status);
    }

    [Fact]
    public async Task Actual_deadline_and_caller_cancellation_never_claim_sent_and_allow_fenced_recovery()
    {
        var (s, source) = await Engineering(); var n = await Queue(s, source); var clock = new Clock();
        var sink = new Sink { Waiting = new(), Release = new() }; var service = Service(s, sink, clock);
        await service.Dispatch(Caller, n.Id, 1, "deadline", default);
        var failed = await service.Get(Caller, n.Id, default); Assert.Equal("Failed", failed.State);
        Assert.Equal("acceptance-unknown", failed.Attempts[0].FailureCode); Assert.Empty(sink.Local.Accepted);
        clock.Advance(); using var cancelled = new CancellationTokenSource(); sink.Waiting = new();
        var running = service.Dispatch(Caller, n.Id, failed.Version, "cancelled", cancelled.Token); await sink.Waiting.Task;
        cancelled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        var claimed = await service.Get(Caller, n.Id, default); Assert.NotNull(claimed.LeaseId); Assert.NotEqual(claimed.LeaseId, claimed.Attempts[^1].Id);
        clock.Advance(); var recovered = await service.Dispatch(Caller, n.Id, claimed.Version, "recover", default);
        Assert.Equal(recovered, await service.Dispatch(Caller, n.Id, claimed.Version, "recover", default));
        Assert.Equal("Failed", (await service.Get(Caller, n.Id, default)).State);
    }
    [Fact]
    public async Task Late_acknowledgement_after_lease_expiry_cannot_commit_and_concurrent_claim_uses_one_key()
    {
        var (s, source) = await Engineering(); var n = await Queue(s, source); var clock = new Clock();
        var sink = new Sink { BeforeAck = () => clock.Advance() }; var service = Service(s, sink, clock);
        await Error(409, () => service.Dispatch(Caller, n.Id, 1, "late", default));
        Assert.Single(sink.Local.Accepted); Assert.Equal("Pending", s.NotificationSnapshot().Notifications.Messages[0].State);
        await Error(409, () => service.Dispatch(Caller, n.Id, 1, "wrong-version", default));
        await service.Dispatch(Caller, n.Id, 2, "recover", default); Assert.Equal("Failed", s.NotificationSnapshot().Notifications.Messages[0].State);
    }
    [Fact]
    public async Task Preference_can_be_explicitly_renewed_after_unrelated_driver_update()
    {
        var (s, _) = await Engineering();
        await s.NotificationExecute(Tenant, tx => { tx.Fleet.Drivers[0] = tx.Fleet.Drivers[0] with { Version = 3 }; return 0; });
        var pref = await Service(s).Preference(Caller, IncidentId, default); Assert.False(pref.Enabled); Assert.Equal(3, pref.DriverVersion);
        await Service(s).Preferences(Caller, IncidentId, pref.DriverVersion, "renew", true, "SYN-RENEWED", default);
        Assert.True((await Service(s).Preference(Caller, IncidentId, default)).Enabled);
    }

}
