using FaultCode.Application.Telemetry;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FaultCode.Application.Fleet;
using FaultCode.Domain;

namespace FaultCode.Application.Incidents;

public sealed class IncidentService(IIncidentStore store, TimeProvider clock, ITelemetry? telemetry = null)
{
    private static void Caller(FleetCaller c)
    {
        if (!FleetRules.CanonicalId(c.TenantId) || !FleetRules.CanonicalId(c.ActorId) || !FleetRules.CanonicalId(c.ClientId)
            || string.IsNullOrWhiteSpace(c.CorrelationId)) throw new IncidentException(403);
    }
    private static Incident Find(IncidentState s, FleetCaller c, Guid id) => s.Incidents.SingleOrDefault(x => x.Id == id && x.TenantId == c.TenantId) ?? throw new IncidentException(404);
    private static bool Visible(Incident i, FleetCaller c) => i.TenantId == c.TenantId && FleetRules.CanonicalId(i.Fleet.OwnerObjectId)
        && (c.AllVehicles || c.OwnVehicles && i.Fleet.OwnerObjectId == c.ActorId);
    public Task<Incident> Get(FleetCaller c, Guid id, CancellationToken ct) =>
        (telemetry ?? NullTelemetry.Instance).Run(TelemetryOperation.Incident, c.CorrelationId, () => GetCore(c, id, ct));
    private async Task<Incident> GetCore(FleetCaller c, Guid id, CancellationToken ct)
    {
        Caller(c); var i = Find(await store.ReadAsync(c.TenantId, ct), c, id);
        return Visible(i, c) ? i : throw new IncidentException(404);
    }
    public async Task<FleetPage<Incident>> List(FleetCaller c, int page, int size, string? status, Guid? vehicleId, CancellationToken ct)
    {
        Caller(c);
        if (page < 1 || size is < 1 or > 100 || status is not null && !IncidentRules.Status(status)) throw new IncidentException(400);
        var all = (await store.ReadAsync(c.TenantId, ct)).Incidents.Where(i => Visible(i, c)
            && (status is null || i.Status == status) && (vehicleId is null || i.Fleet.VehicleId == vehicleId)).OrderBy(i => i.Id).ToArray();
        var offset = ((long)page - 1) * size;
        return new(offset >= all.Length ? [] : all.Skip((int)offset).Take(size).ToArray(), page, size, all.Length);
    }
    private static string Hash(object payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
    private static string ReplayKey(FleetCaller c, string op, string key) => JsonSerializer.Serialize(new[] { c.TenantId, c.ActorId, c.ClientId, op, key });
    private static MutationResult Replay(IncidentReplay r, string hash) => r.Hash == hash ? r.Result : throw new IncidentException(409);
    private static IncidentHistory History(FleetCaller c, long version, string status, string action, DateTimeOffset at, string? failure = null) =>
        new(version, status, action, c.ActorId, c.ClientId, c.CorrelationId, at, failure);
    private static void Audit(IncidentState s, FleetCaller c, string op, MutationResult result, DateTimeOffset now) =>
        s.Audit.Add(new(c.TenantId, c.ActorId, c.ClientId, c.CorrelationId, op, result.Id, result.Version, now));
    public Task<MutationResult> Create(FleetCaller c, IncidentInput input, string key, CancellationToken ct)
    {
        Caller(c);
        if (!IncidentRules.Token(key) || !IncidentRules.Token(input.SourceEventId) || input.VehicleId == Guid.Empty || input.DriverId == Guid.Empty
            || !FaultRules.SyntheticModel(input.Model) || !FaultRules.Software(input.SoftwareVersion)) throw new IncidentException(400);
        string code;
        try { code = FaultRules.Code(input.Code, input.System); } catch (ArgumentException) { throw new IncidentException(400); }
        var canonical = input with { Code = code }; var hash = Hash(canonical);
        var replayKey = ReplayKey(c, "incidents.create", key);
        var sourceKey = JsonSerializer.Serialize(new[] { c.TenantId, c.ClientId, input.SourceEventId });
        return store.TransactAsync(c.TenantId, tx =>
        {
            var s = tx.State;
            if (s.Replays.TryGetValue(replayKey, out var prior)) return Replay(prior, hash);
            if (s.Sources.TryGetValue(sourceKey, out var source))
            {
                var replay = Replay(source, hash); s.Replays.Add(replayKey, source); return replay;
            }
            var now = clock.GetUtcNow();
            if (!IncidentRules.Occurrence(input.OccurredAt, now)) throw new IncidentException(400);
            var context = tx.Context();
            var vehicle = context.Vehicles.SingleOrDefault(v => v.Id == input.VehicleId && v.TenantId == c.TenantId) ?? throw new IncidentException(404);
            var driver = context.Drivers.SingleOrDefault(d => d.Id == input.DriverId && d.TenantId == c.TenantId) ?? throw new IncidentException(404);
            var association = context.Associations.SingleOrDefault(a => a.VehicleId == vehicle.Id && a.DriverId == driver.Id && a.Until is null);
            if (!vehicle.Active || !driver.Active || !FleetRules.CanonicalId(driver.OwnerObjectId) || vehicle.System != input.System
                || association is null || association.From > input.OccurredAt || association.From > now) throw new IncidentException(400);
            var definition = context.Faults.SingleOrDefault(f => f.TenantId == c.TenantId && f.Code == code && f.System == input.System && !f.Retired);
            var publication = definition?.Publications.LastOrDefault();
            IncidentKnowledge? knowledge = publication is null ? null : new(definition!.Id, publication.Version,
                publication.Content with { Remedies = [.. publication.Content.Remedies.Where(r => r.Approved && r.Model == input.Model && r.SoftwareVersion == input.SoftwareVersion)] },
                publication.ApprovedBy, publication.ApprovedAt);
            var id = Guid.NewGuid(); var result = new MutationResult(id, 1);
            var snapshot = new IncidentFleet(vehicle.Id, vehicle.Identifier, vehicle.Version, driver.Id, driver.Identifier, driver.Version, driver.OwnerObjectId, association);
            s.Incidents.Add(new(id, c.TenantId, c.ClientId, input.SourceEventId, code, input.System, input.Model, input.SoftwareVersion,
                input.OccurredAt, now, c.CorrelationId, snapshot, knowledge, "Received", 1,
                [History(c, 1, "Received", "received", now)], [new(Guid.NewGuid(), "investigate", "Pending", 0, null)]));
            var entry = new IncidentReplay(hash, result); s.Sources.Add(sourceKey, entry); s.Replays.Add(replayKey, entry);
            Audit(s, c, "incidents.create", result, now); return result;
        }, ct);
    }
    public Task<MutationResult> Transition(FleetCaller c, Guid id, string status, long version, string key, CancellationToken ct)
    {
        if (!IncidentRules.Status(status)) throw new IncidentException(400);
        return Mutate(c, id, "status", new { status, version }, version, key, (i, now) =>
        {
            if (!IncidentRules.CanTransition(i.Status, status) || i.Work.Any(w => w.State == "Failed")) throw new IncidentException(409);
            var work = i.Status == "Received" ? i.Work : i.Work.Select(w => w.State == "Pending" ? w with { State = "Completed" } : w).ToImmutableArray();
            if (status == "NotificationPending" || status == "Investigating" && i.Status == "Escalated") work = work.Add(new(Guid.NewGuid(), status == "Investigating" ? "investigate" : "notify", "Pending", 0, null));
            return i with { Status = status, Work = work };
        }, null, ct);
    }
    // Lifecycle marker only; FAU-18 owns engineering investigation cases.
    public Task<MutationResult> Escalate(FleetCaller c, Guid id, long version, string key, CancellationToken ct) =>
        Mutate(c, id, "escalate", new { status = "Escalated", version }, version, key, (i, _) =>
        {
            if (!IncidentRules.CanTransition(i.Status, "Escalated") || i.Work.Any(w => w.State == "Failed")) throw new IncidentException(409);
            return i with { Status = "Escalated", Work = [.. i.Work.Select(w => w.State == "Pending" ? w with { State = "Completed" } : w)] };
        }, null, ct);
    public Task<MutationResult> Fail(FleetCaller c, Guid id, string failure, long version, string key, CancellationToken ct)
    {
        if (!IncidentRules.Failure(failure)) throw new IncidentException(400);
        return Mutate(c, id, "failure", new { failure, version }, version, key, (i, now) =>
        {
            var index = Array.FindIndex(i.Work.ToArray(), w => w.State == "Pending"); if (index < 0) throw new IncidentException(409);
            var work = i.Work[index]; return i with { Work = i.Work.SetItem(index, work with { State = "Failed", FailureCode = failure, Attempts = checked(work.Attempts + 1) }) };
        }, failure, ct);
    }
    public Task<MutationResult> Retry(FleetCaller c, Guid id, long version, string key, CancellationToken ct) =>
        Mutate(c, id, "retry", new { version }, version, key, (i, now) =>
        {
            var index = Array.FindIndex(i.Work.ToArray(), w => w.State == "Failed"); if (index < 0) throw new IncidentException(409);
            return i with { Work = i.Work.SetItem(index, i.Work[index] with { State = "Pending", FailureCode = null }) };
        }, null, ct);
    private Task<MutationResult> Mutate(FleetCaller c, Guid id, string op, object payload, long version, string key,
        Func<Incident, DateTimeOffset, Incident> change, string? failure, CancellationToken ct)
    {
        Caller(c); if (!IncidentRules.Token(key) || version < 1) throw new IncidentException(400);
        var replayKey = ReplayKey(c, "incidents." + op + "/" + id, key); var hash = Hash(payload);
        return store.TransactAsync(c.TenantId, tx =>
        {
            var s = tx.State;
            if (s.Replays.TryGetValue(replayKey, out var prior)) return Replay(prior, hash);
            var i = Find(s, c, id); if (i.Version != version) throw new IncidentException(409);
            var now = clock.GetUtcNow(); var next = change(i, now); var result = new MutationResult(id, checked(version + 1));
            next = next with { Version = result.Version, History = i.History.Add(History(c, result.Version, next.Status, op, now, failure)) };
            s.Incidents[s.Incidents.IndexOf(i)] = next; s.Replays.Add(replayKey, new(hash, result)); Audit(s, c, "incidents." + op, result, now);
            return result;
        }, ct);
    }
}
