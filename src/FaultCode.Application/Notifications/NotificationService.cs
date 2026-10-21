using FaultCode.Application.Telemetry;
using System.Collections.Immutable;
using System.Text.Json;
using FaultCode.Application.Fleet;
using FaultCode.Application.Incidents;
using FaultCode.Domain;

namespace FaultCode.Application.Notifications;

public sealed class NotificationService(INotificationStore store, INotificationDelivery delivery, TimeProvider clock, ITelemetry? telemetry = null)
{
    private static void Caller(FleetCaller c)
    {
        if (!FleetRules.CanonicalId(c.TenantId) || !FleetRules.CanonicalId(c.ActorId) || !FleetRules.CanonicalId(c.ClientId)
            || string.IsNullOrWhiteSpace(c.CorrelationId)) throw new IncidentException(403);
    }
    private static bool Visible(Incident i, FleetCaller c) => i.TenantId == c.TenantId && FleetRules.CanonicalId(i.Fleet.OwnerObjectId)
        && (c.AllVehicles || c.OwnVehicles && i.Fleet.OwnerObjectId == c.ActorId)
        && (c.AllDrivers || c.OwnDrivers && i.Fleet.OwnerObjectId == c.ActorId);
    private static Incident Incident(NotificationTransaction tx, FleetCaller c, Guid id) =>
        tx.Context.Incidents.Incidents.SingleOrDefault(i => i.Id == id && Visible(i, c)) ?? throw new IncidentException(404);
    private static Driver Driver(NotificationTransaction tx, FleetCaller c, Guid id) => tx.Fleet.Drivers.SingleOrDefault(d => d.Id == id
        && d.TenantId == c.TenantId && FleetRules.CanonicalId(d.OwnerObjectId) && (c.AllDrivers || c.OwnDrivers && d.OwnerObjectId == c.ActorId)) ?? throw new IncidentException(404);
    private static DriverNotification Message(NotificationTransaction tx, FleetCaller c, Guid id)
    {
        var n = tx.Notifications.Messages.SingleOrDefault(n => n.Id == id && n.TenantId == c.TenantId) ?? throw new IncidentException(404);
        Incident(tx, c, n.IncidentId); return n;
    }
    private static FleetPage<T> Page<T>(T[] items, int page, int size)
    {
        if (page < 1 || size is < 1 or > 100) throw new IncidentException(400);
        var offset = ((long)page - 1) * size;
        return new(offset >= items.Length ? [] : items.Skip((int)offset).Take(size).ToArray(), page, size, items.Length);
    }
    public Task<FleetPage<DriverNotification>> List(FleetCaller c, Guid incidentId, int page, int size, CancellationToken ct) =>
        (telemetry ?? NullTelemetry.Instance).Run(TelemetryOperation.Notification, c.CorrelationId, () => ListCore(c, incidentId, page, size, ct));
    private Task<FleetPage<DriverNotification>> ListCore(FleetCaller c, Guid incidentId, int page, int size, CancellationToken ct)
    {
        Caller(c); return store.Execute(c.TenantId, tx =>
        { Incident(tx, c, incidentId); return Page(tx.Notifications.Messages.Where(n => n.TenantId == c.TenantId && n.IncidentId == incidentId).OrderBy(n => n.CreatedAt).ThenBy(n => n.Id).ToArray(), page, size); }, ct);
    }
    public Task<DriverNotification> Get(FleetCaller c, Guid id, CancellationToken ct) =>
        (telemetry ?? NullTelemetry.Instance).Run(TelemetryOperation.Notification, c.CorrelationId, () => GetCore(c, id, ct));
    private Task<DriverNotification> GetCore(FleetCaller c, Guid id, CancellationToken ct)
    { Caller(c); return store.Execute(c.TenantId, tx => Message(tx, c, id), ct); }
    public Task<FleetPage<NotificationHistory>> History(FleetCaller c, Guid id, int page, int size, CancellationToken ct) =>
        (telemetry ?? NullTelemetry.Instance).Run(TelemetryOperation.Notification, c.CorrelationId, () => HistoryCore(c, id, page, size, ct));
    private Task<FleetPage<NotificationHistory>> HistoryCore(FleetCaller c, Guid id, int page, int size, CancellationToken ct)
    { Caller(c); return store.Execute(c.TenantId, tx => Page(Message(tx, c, id).History.ToArray(), page, size), ct); }
    public Task<LocalNotificationPreference> Preference(FleetCaller c, Guid driverId, CancellationToken ct) =>
        (telemetry ?? NullTelemetry.Instance).Run(TelemetryOperation.Notification, c.CorrelationId, () => PreferenceCore(c, driverId, ct));
    private Task<LocalNotificationPreference> PreferenceCore(FleetCaller c, Guid driverId, CancellationToken ct)
    {
        Caller(c); return store.Execute(c.TenantId, tx =>
        { var d = Driver(tx, c, driverId); var p = tx.Notifications.Preferences.SingleOrDefault(p => p.DriverId == d.Id); return p is null ? new(d.Id, false, null, d.Version) : p with { Enabled = p.Enabled && p.DriverVersion == d.Version, DriverVersion = d.Version }; }, ct);
    }
    private static string Scope(FleetCaller c, string op, string key) => JsonSerializer.Serialize(new[] { c.TenantId, c.ActorId, c.ClientId, op, key });
    private static MutationResult? Replay(NotificationState s, FleetCaller c, string op, string key, object payload)
    {
        if (!IncidentRules.Token(key)) throw new IncidentException(400);
        return s.Replays.TryGetValue(Scope(c, op, key), out var prior)
            ? prior.Payload == JsonSerializer.Serialize(payload) ? prior.Result : throw new IncidentException(409) : null;
    }
    private void Remember(NotificationState s, FleetCaller c, string op, string key, object payload, MutationResult result)
    {
        s.Replays.Add(Scope(c, op, key), new(JsonSerializer.Serialize(payload), result));
        s.Audit.Add(new(c.ActorId, c.ClientId, c.CorrelationId, op, result.Id, result.Version, clock.GetUtcNow()));
    }
    public Task<MutationResult> Preferences(FleetCaller c, Guid driverId, long version, string key, bool enabled, string? contact, CancellationToken ct) =>
        (telemetry ?? NullTelemetry.Instance).Run(TelemetryOperation.Notification, c.CorrelationId, () => PreferencesCore(c, driverId, version, key, enabled, contact, ct));
    private Task<MutationResult> PreferencesCore(FleetCaller c, Guid driverId, long version, string key, bool enabled, string? contact, CancellationToken ct)
    {
        Caller(c); if (version < 1 || contact is not null && !NotificationRules.Contact(contact) || enabled && contact is null) throw new IncidentException(400);
        return store.Execute(c.TenantId, tx =>
        {
            var d = Driver(tx, c, driverId); var payload = new { version, enabled, contact }; var op = "preference/" + driverId;
            if (Replay(tx.Notifications, c, op, key, payload) is { } prior) return prior;
            if (d.Version != version || !d.Active) throw new IncidentException(409);
            var next = checked(version + 1);
            tx.Fleet.Drivers[tx.Fleet.Drivers.IndexOf(d)] = d with { Version = next };
            tx.Notifications.Preferences.RemoveAll(p => p.DriverId == driverId); tx.Notifications.Preferences.Add(new(driverId, enabled, contact, next));
            tx.Fleet.Audit.Add(new(c.TenantId, c.ActorId, c.ClientId, c.CorrelationId, "notification-preference", driverId, clock.GetUtcNow()));
            var result = new MutationResult(driverId, next); Remember(tx.Notifications, c, op, key, payload, result); return result;
        }, ct);
    }
    private static (Driver, LocalNotificationPreference) Recipient(NotificationTransaction tx, FleetCaller c, Incident i)
    {
        var d = Driver(tx, c, i.Fleet.DriverId);
        var v = tx.Fleet.Vehicles.SingleOrDefault(v => v.Id == i.Fleet.VehicleId && v.TenantId == c.TenantId);
        var a = tx.Fleet.Associations.SingleOrDefault(a => a.Id == i.Fleet.Association.Id && a.DriverId == d.Id && a.VehicleId == v?.Id && a.Until is null);
        var p = tx.Notifications.Preferences.SingleOrDefault(p => p.DriverId == d.Id);
        if (!d.Active || v?.Active != true || d.OwnerObjectId != i.Fleet.OwnerObjectId || a is null || p is null || !p.Enabled
            || p.DriverVersion != d.Version || !NotificationRules.Contact(p.Contact)) throw new IncidentException(409);
        return (d, p);
    }
    private sealed record Approved(Guid FaultId, long Publication, long Ontology, FaultRemedy Remedy, ImmutableArray<NotificationEvidence> Evidence, string Kind);
    private static Approved Source(NotificationTransaction tx, FleetCaller c, Incident i, string kind, Guid sourceId)
    {
        Guid faultId; long publication; FaultRemedy? remedy; ImmutableArray<NotificationEvidence> evidence;
        FaultContent content;
        var f = tx.Context.Knowledge.Faults.SingleOrDefault(f => f.TenantId == c.TenantId && f.Code == i.Code && f.System == i.System && !f.Retired);
        var p = f?.Publications.LastOrDefault();
        if (p is null) throw new IncidentException(409);
        content = p.Content; faultId = f!.Id; publication = p.Version;
        if (kind == "investigation")
        {
            var entries = tx.Context.Investigations.Results.Where(e => e.TenantId == c.TenantId && e.Result.IncidentId == i.Id).ToArray();
            var e = entries.SingleOrDefault(e => e.Result.Id == sourceId) ?? throw new IncidentException(404);
            // Actor/client isolation protects original publisher-only investigation content.
            if (e.ActorId != c.ActorId || e.ClientId != c.ClientId) throw new IncidentException(404);
            var r = e.Result;
            if (entries.Any(e => e.Result.CreatedAt > r.CreatedAt || e.Result.IncidentVersion > r.IncidentVersion)
                || r.PolicyVersion != "investigation-v1" || r.Classification is not ("KnownFault" or "KnownFaultWithApprovedPatch")
                || r.Definition?.Id != faultId || r.Definition.Version != publication || r.Remedy is null
                || r.Rule?.PublicationVersion != content.ClassificationVersion || r.Rule.Outcome.ClassificationId != content.ClassificationId
                || !r.Rule.Outcome.RemedyKinds.Contains(r.Remedy.Kind, StringComparer.Ordinal)) throw new IncidentException(409);
            remedy = r.Remedy;
            var patch = r.Classification == "KnownFaultWithApprovedPatch";
            if (patch != (remedy.Patch is not null) || r.ProposedAction.Kind != (patch ? "dealership-referral" : "notification")) throw new IncidentException(409);
            evidence = [.. r.Citations.Select(x => new NotificationEvidence(x.DocumentId, x.SourceId, x.SourceVersion, x.PublicationVersion, x.DocumentChecksum))];
            if (r.Citations.Length == 0 || r.Citations.Any(x => !double.IsFinite(x.Score) || x.Score < .85 || x.Text != remedy.Description
                || !content.References.Any(a => a.Id == x.SourceId && a.Version == x.SourceVersion))) throw new IncidentException(409);
            foreach (var citation in r.Citations)
            {
                var d = tx.Context.Documents.Documents.SingleOrDefault(d => d.Id == citation.DocumentId && d.TenantId == c.TenantId && !d.Deleted);
                var dp = d?.Publications.LastOrDefault();
                if (dp is null || !DocumentRules.Chunks(d!, dp).Any(ch => ch.Id == citation.ChunkId && ch.Page == citation.Page
                    && ch.Offset == citation.Offset && ch.Checksum == citation.Checksum && ch.Text == citation.Text)) throw new IncidentException(409);
            }
        }
        else if (kind == "engineering")
        {
            var ev = tx.Context.Engineering.Events.SingleOrDefault(e => e.Id == sourceId && e.IncidentId == i.Id) ?? throw new IncidentException(404);
            var cs = tx.Context.Engineering.Cases.SingleOrDefault(e => e.Id == ev.CaseId && e.TenantId == c.TenantId);
            var a = cs?.Approvals.LastOrDefault();
            if (cs?.Status != "Resolved" || a is null || a.Id != ev.ApprovalId || a.EventId != ev.Id || a.DriverId != i.Fleet.DriverId
                || ev.DriverId != i.Fleet.DriverId || ev.FaultId != faultId || a.FaultId != faultId || ev.PublicationVersion != publication
                || a.PublicationVersion != publication || a.IncidentId != i.Id || a.OntologyVersion != content.ClassificationVersion) throw new IncidentException(409);
            evidence = [.. a.Evidence.Select(x => new NotificationEvidence(x.DocumentId, x.SourceId, x.SourceVersion, x.PublicationVersion, x.Checksum))];
            remedy = content.Remedies.FirstOrDefault(r => r.Approved && r.Model == i.Model && r.SoftwareVersion == i.SoftwareVersion && r.Patch is not null)
                ?? content.Remedies.FirstOrDefault(r => r.Approved && r.Model == i.Model && r.SoftwareVersion == i.SoftwareVersion);
        }
        else throw new IncidentException(400);
        var ontology = tx.Context.Ontology.Catalog?.Publications.SingleOrDefault(o => o.Version == content.ClassificationVersion);
        var concept = ontology?.Content.Concepts.SingleOrDefault(x => x.Id == content.ClassificationId && x.System == i.System && x.Kind == "classification");
        if (remedy is null || !remedy.Approved || remedy.Model != i.Model || remedy.SoftwareVersion != i.SoftwareVersion
            || !content.Remedies.Contains(remedy) || concept is null || !concept.AllowedRemedyKinds.Contains(remedy.Kind, StringComparer.Ordinal)
            || remedy.Patch is not null && (remedy.Kind != "dealership" || remedy.Patch.SoftwareVersion != i.SoftwareVersion)
            || evidence.Length == 0) throw new IncidentException(409);
        foreach (var pin in evidence)
        {
            var d = tx.Context.Documents.Documents.SingleOrDefault(d => d.Id == pin.DocumentId && d.TenantId == c.TenantId && !d.Deleted && d.SourceId == pin.SourceId);
            var dp = d?.Publications.LastOrDefault();
            if (dp is null || dp.Version != pin.PublicationVersion || dp.Checksum != pin.Checksum || dp.Content.SourceVersion != pin.SourceVersion
                || dp.Content.Access != "tenant" || dp.Content.Model != i.Model || dp.Content.SoftwareVersion != i.SoftwareVersion || dp.Content.System != i.System
                || !content.References.Any(x => x.Id == pin.SourceId && x.Version == pin.SourceVersion)) throw new IncidentException(409);
        }
        return new(faultId, publication, content.ClassificationVersion, remedy, evidence, remedy.Patch is null ? "fault-guidance" : "dealership-referral");
    }
    private Incident Transition(NotificationTransaction tx, FleetCaller c, Incident i, string status, string action)
    {
        var next = checked(i.Version + 1); var at = clock.GetUtcNow();
        var changed = i with
        {
            Status = status,
            Version = next,
            History = i.History.Add(new(next, status, action, c.ActorId, c.ClientId, c.CorrelationId, at, null)),
            Work = [.. i.Work.Select(w => w.State is "Pending" or "Failed" ? w with { State = "Completed", FailureCode = null } : w)]
        };
        if (status == "NotificationPending") changed = changed with { Work = changed.Work.Add(new(Guid.NewGuid(), "notify", "Pending", 0, null)) };
        tx.Context.Incidents.Incidents[tx.Context.Incidents.Incidents.IndexOf(i)] = changed;
        tx.Context.Incidents.Audit.Add(new(c.TenantId, c.ActorId, c.ClientId, c.CorrelationId, action, i.Id, next, at)); return changed;
    }
    private DriverNotification Record(DriverNotification n, FleetCaller c, string action) => n with
    { History = n.History.Add(new(n.Version, n.State, action, clock.GetUtcNow(), c.ActorId, c.ClientId, c.CorrelationId)) };
    private static void Save(NotificationTransaction tx, DriverNotification old, DriverNotification changed) =>
        tx.Notifications.Messages[tx.Notifications.Messages.IndexOf(old)] = changed;
    public Task<MutationResult> Enqueue(FleetCaller c, Guid incidentId, string kind, Guid sourceId, long version, string key, CancellationToken ct) =>
        (telemetry ?? NullTelemetry.Instance).Run(TelemetryOperation.Notification, c.CorrelationId, () => EnqueueCore(c, incidentId, kind, sourceId, version, key, ct));
    private Task<MutationResult> EnqueueCore(FleetCaller c, Guid incidentId, string kind, Guid sourceId, long version, string key, CancellationToken ct)
    {
        Caller(c); if (version < 1 || sourceId == Guid.Empty || kind is not ("investigation" or "engineering")) throw new IncidentException(400);
        return store.Execute(c.TenantId, tx =>
        {
            var i = Incident(tx, c, incidentId); var op = "enqueue/" + incidentId; var payload = new { kind, sourceId, version };
            if (Replay(tx.Notifications, c, op, key, payload) is { } prior) return prior;
            var duplicate = tx.Notifications.Messages.SingleOrDefault(n => n.TenantId == c.TenantId && n.SourceKind == kind && n.SourceId == sourceId);
            if (duplicate is not null)
            {
                if (duplicate.IncidentId != incidentId) throw new IncidentException(409);
                var found = new MutationResult(duplicate.Id, duplicate.Version); Remember(tx.Notifications, c, op, key, payload, found); return found;
            }
            if (i.Version != version || tx.Notifications.Messages.Any(n => n.IncidentId == i.Id && n.TenantId == c.TenantId && n.State is "Pending" or "Failed" or "Sent")) throw new IncidentException(409);
            var (d, pref) = Recipient(tx, c, i); var approved = Source(tx, c, i, kind, sourceId);
            if (kind == "investigation")
            {
                var result = tx.Context.Investigations.Results.Single(e => e.Result.Id == sourceId && e.TenantId == c.TenantId).Result;
                if (i.Status != "Investigating" || result.IncidentVersion != i.Version) throw new IncidentException(409);
                i = Transition(tx, c, i, "Resolved", "notification-approved-investigation");
            }
            else
            {
                var ev = tx.Context.Engineering.Events.Single(e => e.Id == sourceId);
                if (i.Status != "Resolved" || ev.IncidentVersion != i.Version) throw new IncidentException(409);
            }
            i = Transition(tx, c, i, "NotificationPending", "notification-enqueued");
            var n = new DriverNotification(Guid.NewGuid(), c.TenantId, i.Id, d.Id, i.Fleet.OwnerObjectId, kind, sourceId, approved.FaultId,
                approved.Publication, approved.Ontology, approved.Remedy, approved.Evidence, NotificationRules.Template,
                NotificationRules.Render(i.Id, approved.Kind, approved.Remedy), pref.Contact!, d.Version, i.Version, "Pending", 1, clock.GetUtcNow(), null, null, null, [], []);
            tx.Notifications.Messages.Add(Record(n, c, "enqueued")); var resultMutation = new MutationResult(n.Id, 1);
            Remember(tx.Notifications, c, op, key, payload, resultMutation); return resultMutation;
        }, ct);
    }
    public Task<MutationResult> Cancel(FleetCaller c, Guid id, long version, string key, CancellationToken ct) =>
        (telemetry ?? NullTelemetry.Instance).Run(TelemetryOperation.Notification, c.CorrelationId, () => CancelCore(c, id, version, key, ct));
    private Task<MutationResult> CancelCore(FleetCaller c, Guid id, long version, string key, CancellationToken ct)
    {
        Caller(c); if (version < 1) throw new IncidentException(400);
        return store.Execute(c.TenantId, tx =>
        {
            var n = Message(tx, c, id); var op = "cancel/" + id; var payload = new { version };
            if (Replay(tx.Notifications, c, op, key, payload) is { } prior) return prior;
            if (n.Version != version || n.LeaseId is not null || n.State is "Cancelled" or "Superseded") throw new IncidentException(409);
            var i = Incident(tx, c, n.IncidentId);
            if (i.Status is not ("NotificationPending" or "Notified") || i.Version != n.IncidentVersion) throw new IncidentException(409);
            Transition(tx, c, i, "Resolved", "notification-cancelled");
            var changed = Record(n with { State = n.State == "Sent" ? "Superseded" : "Cancelled", Version = checked(n.Version + 1), NextAttemptAt = null }, c, "cancelled");
            Save(tx, n, changed); var r = new MutationResult(id, changed.Version); Remember(tx.Notifications, c, op, key, payload, r); return r;
        }, ct);
    }
    private sealed record Claim(DriverNotification? Message, MutationResult? Replay, Guid Lease);
    public Task<MutationResult> Dispatch(FleetCaller c, Guid id, long version, string key, CancellationToken ct) =>
        (telemetry ?? NullTelemetry.Instance).Run(TelemetryOperation.Notification, c.CorrelationId, () => DispatchCore(c, id, version, key, ct));
    private async Task<MutationResult> DispatchCore(FleetCaller c, Guid id, long version, string key, CancellationToken ct)
    {
        Caller(c); if (version < 1) throw new IncidentException(400);
        var op = "dispatch/" + id; var payload = new { version };
        var claim = await store.Execute(c.TenantId, tx =>
        {
            var n = Message(tx, c, id);
            if (Replay(tx.Notifications, c, op, key, payload) is { } prior) return new Claim(null, prior, Guid.Empty);
            var now = clock.GetUtcNow();
            if (n.Version != version) throw new IncidentException(409);
            if (n.LeaseId is not null)
            {
                if (n.LeaseUntil > now) throw new IncidentException(409);
                // Persist recovery without sending: unknown provider acceptance must remain visible.
                var attempts = n.Attempts.SetItem(n.Attempts.Length - 1, n.Attempts[^1] with { State = "Failed", CompletedAt = now, FailureCode = "acceptance-unknown" });
                var expired = Record(n with
                {
                    State = "Failed",
                    Version = checked(n.Version + 1),
                    LeaseId = null,
                    LeaseUntil = null,
                    NextAttemptAt = now.AddSeconds(Math.Min(300, Math.Pow(2, attempts.Length))),
                    Attempts = attempts
                }, c, "lease-expired");
                var recovered = new MutationResult(id, expired.Version); Save(tx, n, expired); Remember(tx.Notifications, c, op, key, payload, recovered); return new Claim(null, recovered, Guid.Empty);
            }
            if (n.Version != version || n.State is not ("Pending" or "Failed") || n.Attempts.Length >= 5 || n.NextAttemptAt > now) throw new IncidentException(409);
            var i = Incident(tx, c, n.IncidentId); var (d, pref) = Recipient(tx, c, i);
            if (i.Status != "NotificationPending" || i.Version != n.IncidentVersion || d.Version != n.DriverVersion || pref.Contact != n.Contact) throw new IncidentException(409);
            var approved = Source(tx, c, i, n.SourceKind, n.SourceId);
            if (approved.FaultId != n.FaultId || approved.Publication != n.PublicationVersion || approved.Ontology != n.OntologyVersion
                || approved.Remedy != n.Remedy || !approved.Evidence.SequenceEqual(n.Evidence)) throw new IncidentException(409);
            var lease = Guid.NewGuid();
            var changed = Record(n with
            {
                Version = checked(n.Version + 1),
                LeaseId = lease,
                LeaseUntil = now.AddSeconds(30),
                Attempts = n.Attempts.Add(new(Guid.NewGuid(), n.Attempts.Length + 1, now, null, "Pending", null, null))
            }, c, "claimed");
            Save(tx, n, changed); return new Claim(changed, null, lease);
        }, ct);
        if (claim.Replay is { } replay) return replay;
        var message = claim.Message!; string? receipt = null; string? failure = null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(5));
        using (var attempt = (telemetry ?? NullTelemetry.Instance).Begin(TelemetryOperation.Delivery, c.CorrelationId))
        {
            try
            {
                var acknowledgement = await delivery.Send(new(id, message.Contact, message.Message), deadline.Token).WaitAsync(deadline.Token);
                if (!FaultRules.Text(acknowledgement?.Receipt, 100)) failure = "invalid-acknowledgement";
                else receipt = acknowledgement!.Receipt;
            }
            catch (OperationCanceledException) { failure = "acceptance-unknown"; }
            catch (Exception) { failure = "delivery-unavailable"; }
            if (receipt is null)
            {
                attempt.Report(failure == "acceptance-unknown" ? TelemetryError.Cancelled : TelemetryError.Dependency);
                attempt.Complete(failure == "acceptance-unknown" ? TelemetryOutcome.Cancelled : TelemetryOutcome.Failed);
            }
            else attempt.Complete(TelemetryOutcome.Success);
        }
        // A cancelled request leaves the committed lease recoverable; never abandon an in-flight store callback.
        ct.ThrowIfCancellationRequested();
        return await store.Execute(c.TenantId, tx =>
        {
            var n = Message(tx, c, id); var now = clock.GetUtcNow();
            if (n.LeaseId != claim.Lease || n.LeaseUntil <= now) throw new IncidentException(409);
            var i = Incident(tx, c, n.IncidentId);
            var changed = n with
            {
                Version = checked(n.Version + 1),
                State = receipt is null ? "Failed" : "Sent",
                LeaseId = null,
                LeaseUntil = null,
                NextAttemptAt = receipt is null ? now.AddSeconds(Math.Min(300, Math.Pow(2, n.Attempts.Length))) : null,
                Attempts = n.Attempts.SetItem(n.Attempts.Length - 1, n.Attempts[^1] with { CompletedAt = now, State = receipt is null ? "Failed" : "Sent", FailureCode = failure, Acknowledgement = receipt })
            };
            if (receipt is not null && i.Status == "NotificationPending" && i.Version == n.IncidentVersion)
                changed = changed with { IncidentVersion = Transition(tx, c, i, "Notified", "notification-acknowledged").Version };
            changed = Record(changed, c, receipt is null ? "failed" : "acknowledged"); Save(tx, n, changed);
            var result = new MutationResult(id, changed.Version); Remember(tx.Notifications, c, op, key, payload, result); return result;
        }, ct);
    }
}
