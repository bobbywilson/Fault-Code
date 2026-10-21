using System.Collections.Immutable;
using System.Text.Json;
using FaultCode.Application.Fleet;
using FaultCode.Application.Incidents;
using FaultCode.Domain;

namespace FaultCode.Application.Escalations;

public sealed class EscalationService(IEscalationStore store, TimeProvider clock)
{
    private static void Caller(FleetCaller c)
    {
        if (!FleetRules.CanonicalId(c.TenantId) || !FleetRules.CanonicalId(c.ActorId) || !FleetRules.CanonicalId(c.ClientId)
            || string.IsNullOrWhiteSpace(c.CorrelationId)) throw new IncidentException(403);
    }
    private static bool Visible(Incident i, FleetCaller c) => i.TenantId == c.TenantId && FleetRules.CanonicalId(i.Fleet.OwnerObjectId)
        && (c.AllVehicles || c.OwnVehicles && i.Fleet.OwnerObjectId == c.ActorId);
    private static Incident Incident(EngineeringTransaction tx, FleetCaller c, Guid id) =>
        tx.Incidents.Incidents.SingleOrDefault(i => i.Id == id && Visible(i, c)) ?? throw new IncidentException(404);
    private static EngineeringCase Case(EngineeringTransaction tx, FleetCaller c, Guid id)
    {
        var e = tx.Engineering.Cases.SingleOrDefault(e => e.Id == id && e.TenantId == c.TenantId) ?? throw new IncidentException(404);
        Incident(tx, c, e.IncidentId); return e;
    }
    private static FleetPage<T> Page<T>(T[] all, int page, int size)
    {
        if (page < 1 || size is < 1 or > 100) throw new IncidentException(400);
        var offset = ((long)page - 1) * size;
        return new(offset >= all.Length ? [] : all.Skip((int)offset).Take(size).ToArray(), page, size, all.Length);
    }
    public Task<FleetPage<EngineeringCase>> List(FleetCaller c, int page, int size, string? status, CancellationToken ct)
    {
        Caller(c); if (status is not null && !EngineeringRules.Status(status)) throw new IncidentException(400);
        return store.Execute(c.TenantId, tx => Page(tx.Engineering.Cases.Where(e => e.TenantId == c.TenantId && (status is null || e.Status == status)
            && tx.Incidents.Incidents.Any(i => i.Id == e.IncidentId && Visible(i, c))).OrderBy(e => e.Id).ToArray(), page, size), ct);
    }
    public Task<EngineeringCase> Get(FleetCaller c, Guid id, CancellationToken ct)
    { Caller(c); return store.Execute(c.TenantId, tx => Case(tx, c, id), ct); }
    public Task<FleetPage<EngineeringHistory>> History(FleetCaller c, Guid id, int page, int size, CancellationToken ct)
    { Caller(c); return store.Execute(c.TenantId, tx => Page(Case(tx, c, id).History.ToArray(), page, size), ct); }
    public Task<FleetPage<EngineeringEvent>> Events(FleetCaller c, Guid id, int page, int size, CancellationToken ct)
    {
        Caller(c); return store.Execute(c.TenantId, tx =>
        { Case(tx, c, id); return Page(tx.Engineering.Events.Where(e => e.CaseId == id).OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToArray(), page, size); }, ct);
    }
    private MutationResult Replay(EngineeringState s, FleetCaller c, string op, string key, object payload, Func<MutationResult> action)
    {
        if (!IncidentRules.Token(key)) throw new IncidentException(400);
        var scope = JsonSerializer.Serialize(new[] { c.TenantId, c.ActorId, c.ClientId, op, key });
        var fingerprint = JsonSerializer.Serialize(payload);
        if (s.Replays.TryGetValue(scope, out var prior)) return prior.Payload == fingerprint ? prior.Result : throw new IncidentException(409);
        var result = action(); s.Replays.Add(scope, new(fingerprint, result)); return result;
    }
    private void Audit(EngineeringState s, FleetCaller c, string op, MutationResult r) =>
        s.Audit.Add(new(c.ActorId, c.ClientId, c.CorrelationId, op, r.Id, r.Version, clock.GetUtcNow()));
    public Task<MutationResult> Create(FleetCaller c, Guid incidentId, Guid investigationId, long version, string key, CancellationToken ct)
    {
        Caller(c); if (incidentId == Guid.Empty || investigationId == Guid.Empty || version < 1) throw new IncidentException(400);
        return store.Execute(c.TenantId, tx =>
        {
            var i = Incident(tx, c, incidentId);
            return Replay(tx.Engineering, c, "engineering.create", key, new { incidentId, investigationId, version }, () =>
            {
                if (i.Version != version) throw new IncidentException(409);
                var existing = tx.Engineering.Cases.SingleOrDefault(e => e.TenantId == c.TenantId && e.IncidentId == incidentId);
                if (existing is not null) return new(existing.Id, existing.Version);
                var result = tx.Investigations.Results.SingleOrDefault(r => r.TenantId == c.TenantId && r.Result.Id == investigationId
                    && r.Result.IncidentId == incidentId)?.Result ?? throw new IncidentException(404);
                if (i.Status is not ("Investigating" or "Escalated") || result.IncidentVersion > i.Version
                    || result.Classification is not ("UnknownFault" or "InsufficientEvidence")) throw new IncidentException(409);
                if (result.Classification == "UnknownFault" && tx.Knowledge.Faults.Any(f => f.TenantId == c.TenantId && f.Code == i.Code
                    && !f.Retired && f.Publications.Length > 0)) throw new IncidentException(409);
                var now = clock.GetUtcNow(); var id = Guid.NewGuid();
                var e = new EngineeringCase(id, c.TenantId, incidentId, investigationId, result.Classification, result.IncidentVersion,
                    "Open", 1, null, null, [], [], []);
                e = e with { History = [Entry(e, c, "created", now)] };
                tx.Engineering.Cases.Add(e); var mutation = new MutationResult(id, 1); Audit(tx.Engineering, c, "created", mutation); return mutation;
            });
        }, ct);
    }
    private static EngineeringHistory Entry(EngineeringCase e, FleetCaller c, string action, DateTimeOffset now,
        EngineeringNote? note = null, Guid? approvalId = null) =>
        new(e.Version, e.Status, action, e.AssigneeId, note, e.Proposal, approvalId, c.ActorId, c.ClientId, c.CorrelationId, now);
    private Task<MutationResult> Change(FleetCaller c, Guid id, long version, string key, string op, object payload,
        Func<EngineeringTransaction, EngineeringCase, EngineeringCase> action, CancellationToken ct)
    {
        Caller(c); if (version < 1) throw new IncidentException(400);
        return store.Execute(c.TenantId, tx =>
        {
            var e = Case(tx, c, id);
            return Replay(tx.Engineering, c, op + "/" + id, key, new { version, payload }, () =>
            {
                if (e.Version != version) throw new IncidentException(409);
                var changed = action(tx, e with { Version = checked(e.Version + 1) }); ct.ThrowIfCancellationRequested();
                tx.Engineering.Cases[tx.Engineering.Cases.IndexOf(e)] = changed;
                var r = new MutationResult(id, changed.Version); Audit(tx.Engineering, c, op, r); return r;
            });
        }, ct);
    }
    public Task<MutationResult> Assign(FleetCaller c, Guid id, long version, string key, string? assignee, CancellationToken ct)
    {
        if (assignee is not null && !FleetRules.CanonicalId(assignee)) throw new IncidentException(400);
        return Change(c, id, version, key, "assigned", new { assignee }, (_, e) =>
        {
            if (e.Status == "Resolved") throw new IncidentException(409);
            e = e with { AssigneeId = assignee }; return e with { History = e.History.Add(Entry(e, c, "assigned", clock.GetUtcNow())) };
        }, ct);
    }
    public Task<MutationResult> Note(FleetCaller c, Guid id, long version, string key, string text, CancellationToken ct)
    {
        if (!FaultRules.Text(text, 2000)) throw new IncidentException(400);
        return Change(c, id, version, key, "noted", new { text }, (_, e) =>
        {
            var note = new EngineeringNote(Guid.NewGuid(), text, c.ActorId, c.ClientId, clock.GetUtcNow());
            return e with { Notes = e.Notes.Add(note), History = e.History.Add(Entry(e, c, "noted", note.At, note)) };
        }, ct);
    }
    public Task<MutationResult> Status(FleetCaller c, Guid id, long version, string key, string status, CancellationToken ct)
    {
        if (!EngineeringRules.Status(status)) throw new IncidentException(400);
        return Change(c, id, version, key, "transitioned", new { status }, (tx, e) =>
        {
            if (!EngineeringRules.Transition(e.Status, status)) throw new IncidentException(409);
            if (status == "Reopened")
            {
                var i = Incident(tx, c, e.IncidentId);
                if (i.Status != "Resolved") throw new IncidentException(409);
                TransitionIncident(tx, c, i, "Investigating", "engineering-reopened");
            }
            e = e with { Status = status, Proposal = null };
            return e with { History = e.History.Add(Entry(e, c, "transitioned", clock.GetUtcNow())) };
        }, ct);
    }
    private static FaultDefinition? Definition(EngineeringTransaction tx, FleetCaller c, Incident i) =>
        tx.Knowledge.Faults.SingleOrDefault(f => f.TenantId == c.TenantId && f.Code == i.Code);
    private static ImmutableArray<ResolutionEvidence> Validate(EngineeringTransaction tx, FleetCaller c, Incident i, FaultContent content, long expected)
    {
        try { FaultRules.Validate(content); } catch (ArgumentException) { throw new IncidentException(400); }
        var f = Definition(tx, c, i);
        if (expected < 0) throw new IncidentException(400);
        if (f is null ? expected != 0 : f.Version != expected || f.Retired || f.System != i.System) throw new IncidentException(409);
        var publication = tx.Ontology.Catalog?.Publications.SingleOrDefault(p => p.Version == content.ClassificationVersion);
        var concept = publication?.Content.Concepts.SingleOrDefault(x => x.Id == content.ClassificationId && x.Kind == "classification" && x.System == i.System);
        if (concept is null || !content.Remedies.All(r => concept.AllowedRemedyKinds.Contains(r.Kind, StringComparer.Ordinal))
            || !content.Remedies.Any(r => r.Approved && r.Model == i.Model && r.SoftwareVersion == i.SoftwareVersion)) throw new IncidentException(400);
        var evidence = ImmutableArray.CreateBuilder<ResolutionEvidence>();
        foreach (var reference in content.References)
        {
            var d = tx.Documents.Documents.SingleOrDefault(d => d.TenantId == c.TenantId && d.SourceId == reference.Id && !d.Deleted);
            var p = d?.Publications.LastOrDefault();
            if (p is null || p.Content.SourceVersion != reference.Version || p.Content.System != i.System || p.Content.Model != i.Model
                || p.Content.SoftwareVersion != i.SoftwareVersion || p.Content.Access != "tenant" && p.ApprovedBy != c.ActorId) throw new IncidentException(404);
            evidence.Add(new(d!.Id, d.SourceId, reference.Version, p.Version, p.Checksum));
        }
        return evidence.ToImmutable();
    }
    public Task<MutationResult> Propose(FleetCaller c, Guid id, long version, string key, FaultContent content, long expectedFaultVersion, CancellationToken ct) =>
        Change(c, id, version, key, "proposed", new { content, expectedFaultVersion }, (tx, e) =>
        {
            if (e.Status != "Investigating") throw new IncidentException(409);
            var i = Incident(tx, c, e.IncidentId);
            if (i.Status is not ("Investigating" or "Escalated")) throw new IncidentException(409);
            var evidence = Validate(tx, c, i, content, expectedFaultVersion);
            e = e with { Status = "AwaitingApproval", Proposal = new(Guid.NewGuid(), content, expectedFaultVersion, i.Version, c.ActorId, clock.GetUtcNow(), evidence) };
            return e with { History = e.History.Add(Entry(e, c, "proposed", clock.GetUtcNow())) };
        }, ct);
    public Task<MutationResult> Approve(FleetCaller c, Guid id, long version, string key, CancellationToken ct) =>
        Change(c, id, version, key, "approved", new { }, (tx, e) =>
        {
            if (e.Status != "AwaitingApproval" || e.Proposal is not { } proposal) throw new IncidentException(409);
            var i = Incident(tx, c, e.IncidentId);
            if (i.Version != proposal.IncidentVersion || i.Status is not ("Investigating" or "Escalated")) throw new IncidentException(409);
            var evidence = Validate(tx, c, i, proposal.Content, proposal.ExpectedFaultVersion);
            if (!evidence.SequenceEqual(proposal.Evidence)) throw new IncidentException(409);
            var now = clock.GetUtcNow(); var f = Definition(tx, c, i);
            var faultId = f?.Id ?? Guid.NewGuid(); var next = checked((f?.Version ?? 0) + 1);
            var revision = new FaultRevision(next, proposal.Content, "published", c.ActorId, now);
            var publication = new FaultPublication(next, proposal.Content, c.ActorId, now);
            var published = f is null ? new FaultDefinition(faultId, c.TenantId, i.Code, i.System, false, next, proposal.Content, [revision], [publication])
                : f with { Version = next, Draft = proposal.Content, History = f.History.Add(revision), Publications = f.Publications.Add(publication) };
            if (f is null) tx.Knowledge.Faults.Add(published); else tx.Knowledge.Faults[tx.Knowledge.Faults.IndexOf(f)] = published;
            tx.Knowledge.Audit.Add(new(c.TenantId, c.ActorId, c.ClientId, c.CorrelationId, "engineering.publish", faultId, next, now));
            var resolved = TransitionIncident(tx, c, i, "Resolved", "engineering-resolved");
            var eventId = Guid.NewGuid(); var approval = new EngineeringApproval(Guid.NewGuid(), proposal.Id, faultId, next,
                proposal.Content.ClassificationVersion, evidence, i.Id, i.Fleet.DriverId, c.ActorId, now, eventId);
            tx.Engineering.Events.Add(new(eventId, e.Id, approval.Id, i.Id, resolved.Version, i.Fleet.DriverId, faultId, next, now, "Pending"));
            e = e with { Status = "Resolved", Proposal = null, Approvals = e.Approvals.Add(approval) };
            return e with { History = e.History.Add(Entry(e, c, "approved", now, approvalId: approval.Id)) };
        }, ct);
    private Incident TransitionIncident(EngineeringTransaction tx, FleetCaller c, Incident i, string status, string action)
    {
        var now = clock.GetUtcNow(); var next = checked(i.Version + 1);
        var changed = i with
        {
            Version = next,
            Status = status,
            History = i.History.Add(new(next, status, action, c.ActorId, c.ClientId, c.CorrelationId, now, null)),
            Work = [.. i.Work.Select(w => w.State == "Pending" ? w with { State = "Completed" } : w)]
        };
        tx.Incidents.Incidents[tx.Incidents.Incidents.IndexOf(i)] = changed;
        tx.Incidents.Audit.Add(new(c.TenantId, c.ActorId, c.ClientId, c.CorrelationId, action, i.Id, next, now)); return changed;
    }
}
