using System.Text.Json;
using FaultCode.Application.Fleet;
using FaultCode.Application.Notifications;
using FaultCode.Domain;

namespace FaultCode.Application.Incidents;

public sealed record IncidentUpdateResult(MutationResult Mutation, Guid? NotificationId);

// Resource mutations share the same durable transaction as dispatch, notifications and replay.
public sealed class ResourceMutationService(INotificationStore store, TimeProvider clock)
{
    private static void Validate(FleetCaller c, long version, string key)
    {
        if (!FleetRules.CanonicalId(c.TenantId) || !FleetRules.CanonicalId(c.ActorId) || !FleetRules.CanonicalId(c.ClientId)) throw new IncidentException(403);
        if (version < 1 || !IncidentRules.Token(key)) throw new IncidentException(400);
    }
    private static bool Visible(Incident i, FleetCaller c) => !i.Deleted && i.TenantId == c.TenantId
        && (c.AllVehicles || c.OwnVehicles && i.Fleet.OwnerObjectId == c.ActorId)
        && (c.AllDrivers || c.OwnDrivers && i.Fleet.OwnerObjectId == c.ActorId);
    private static Incident Incident(NotificationTransaction tx, FleetCaller c, Guid id) =>
        tx.Context.Incidents.Incidents.SingleOrDefault(i => i.Id == id && Visible(i, c)) ?? throw new IncidentException(404);
    private static DriverNotification Notification(NotificationTransaction tx, FleetCaller c, Guid id)
    {
        var n = tx.Notifications.Messages.SingleOrDefault(n => !n.Deleted && n.Id == id && n.TenantId == c.TenantId) ?? throw new IncidentException(404);
        Incident(tx, c, n.IncidentId); return n;
    }
    private static string Scope(FleetCaller c, string op, string key) => JsonSerializer.Serialize(new[] { c.TenantId, c.ActorId, c.ClientId, op, key });
    private static MutationResult? Replay(NotificationTransaction tx, string scope, string payload) =>
        tx.Notifications.Replays.TryGetValue(scope, out var r) ? r.Payload == payload ? r.Result : throw new IncidentException(409) : null;
    private void Remember(NotificationTransaction tx, FleetCaller c, string scope, string payload, string op, MutationResult result)
    {
        tx.Notifications.Replays.Add(scope, new(payload, result));
        tx.Notifications.Audit.Add(new(c.ActorId, c.ClientId, c.CorrelationId, op, result.Id, result.Version, clock.GetUtcNow()));
    }
    private static void Idle(DriverNotification n, DateTimeOffset now)
    { if (n.State is "Sending" or "DeliveryUnknown" || n.LeaseUntil > now) throw new IncidentException(409); }
    private DriverNotification Draft(NotificationTransaction tx, FleetCaller c, Incident i, string kind)
    {
        var d = tx.Fleet.Drivers.SingleOrDefault(d => d.Id == i.Fleet.DriverId && d.TenantId == c.TenantId && d.Active) ?? throw new IncidentException(409);
        if (d.Email is null || !NotificationRules.Contact(d.Email) || d.OwnerObjectId != i.Fleet.OwnerObjectId) throw new IncidentException(409);
        var now = clock.GetUtcNow(); var id = Guid.NewGuid();
        var n = new DriverNotification(id, c.TenantId, i.Id, d.Id, d.OwnerObjectId, kind, i.Id, Guid.Empty, 0, 0,
            new("awaiting-investigation", "guidance", "Awaiting investigation", false, i.Model, i.SoftwareVersion, null), [],
            "vehicle-dispatch-v1", "", d.Email, d.Version, i.Version, "AwaitingAgent", 1, now, null, null, null, [],
            [new(1, "AwaitingAgent", "created", now, c.ActorId, c.ClientId, c.CorrelationId)]);
        tx.Notifications.Messages.Add(n); return n;
    }
    public Task<MutationResult> CreateNotification(FleetCaller c, Guid incidentId, string key, CancellationToken ct)
    {
        Validate(c, 1, key); var scope = Scope(c, "notifications.create", key); var payload = JsonSerializer.Serialize(new { incidentId });
        return store.Execute(c.TenantId, tx =>
        {
            if (Replay(tx, scope, payload) is { } prior) return prior;
            var i = Incident(tx, c, incidentId); var kind = i.Status == "Resolved" ? "incident-resolution" : "vehicle-dispatch";
            var n = tx.Notifications.Messages.SingleOrDefault(n => !n.Deleted && n.TenantId == c.TenantId && n.IncidentId == i.Id && n.SourceKind == kind);
            if (n is not null) throw new IncidentException(409);
            n = Draft(tx, c, i, kind); var result = new MutationResult(n.Id, n.Version);
            Remember(tx, c, scope, payload, "notifications.create", result); return result;
        }, ct);
    }
    public Task<MutationResult> UpdateNotification(FleetCaller c, Guid id, string state, long version, string key, CancellationToken ct)
    {
        Validate(c, version, key);
        if (state is not ("AwaitingAgent" or "Cancelled")) throw new IncidentException(400);
        var scope = Scope(c, "notifications.update/" + id, key); var payload = JsonSerializer.Serialize(new { state, version });
        return store.Execute(c.TenantId, tx =>
        {
            if (Replay(tx, scope, payload) is { } prior) return prior;
            var n = Notification(tx, c, id); var now = clock.GetUtcNow(); Idle(n, now);
            if (n.Version != version || n.State is "Sent" or "Superseded" || state == "AwaitingAgent" && (n.Attempts.Length >= 5 || n.NextAttemptAt > now)) throw new IncidentException(409);
            var next = n with
            {
                State = state,
                Version = checked(version + 1),
                LeaseId = null,
                LeaseUntil = null,
                NextAttemptAt = null,
                History = n.History.Add(new(version + 1, state, "updated", now, c.ActorId, c.ClientId, c.CorrelationId))
            };
            tx.Notifications.Messages[tx.Notifications.Messages.IndexOf(n)] = next; var result = new MutationResult(id, next.Version);
            Remember(tx, c, scope, payload, "notifications.update", result); return result;
        }, ct);
    }
    public Task<MutationResult> DeleteNotification(FleetCaller c, Guid id, long version, string key, CancellationToken ct)
    {
        Validate(c, version, key); var scope = Scope(c, "notifications.delete/" + id, key); var payload = JsonSerializer.Serialize(new { version });
        return store.Execute(c.TenantId, tx =>
        {
            if (Replay(tx, scope, payload) is { } prior) return prior;
            var n = Notification(tx, c, id); var now = clock.GetUtcNow(); Idle(n, now);
            if (n.Version != version) throw new IncidentException(409);
            var next = n with
            {
                Deleted = true,
                Version = checked(version + 1),
                NextAttemptAt = null,
                History = n.History.Add(new(version + 1, n.State, "deleted", now, c.ActorId, c.ClientId, c.CorrelationId))
            };
            tx.Notifications.Messages[tx.Notifications.Messages.IndexOf(n)] = next; var result = new MutationResult(id, next.Version);
            Remember(tx, c, scope, payload, "notifications.delete", result); return result;
        }, ct);
    }
    public Task<IncidentUpdateResult> UpdateIncident(FleetCaller c, Guid id, string status, long version, string key, CancellationToken ct, string? resolution = null)
    {
        Validate(c, version, key); if (!IncidentRules.Status(status)) throw new IncidentException(400);
        resolution = resolution?.Trim();
        if (status == "Resolved" ? string.IsNullOrWhiteSpace(resolution) || resolution.Length > 2000
            || resolution.Any(ch => char.IsControl(ch) && ch is not ('\r' or '\n' or '\t')) : resolution is not null) throw new IncidentException(400);
        var scope = Scope(c, "incidents.update/" + id, key); var payload = JsonSerializer.Serialize(new { status, version, resolution });
        return store.Execute(c.TenantId, tx =>
        {
            if (Replay(tx, scope, payload) is { } prior)
                return new IncidentUpdateResult(prior, tx.Notifications.Messages.SingleOrDefault(n => n.IncidentId == id && n.SourceKind == "incident-resolution" && !n.Deleted)?.Id);
            var i = Incident(tx, c, id); var now = clock.GetUtcNow();
            if (i.Version != version || i.Status == status || !IncidentRules.CanTransition(i.Status, status) && !(i.Status == "Received" && status == "Resolved")) throw new IncidentException(409);
            foreach (var n in tx.Notifications.Messages.Where(n => !n.Deleted && n.IncidentId == id)) Idle(n, now);
            var next = i with
            {
                Status = status,
                Version = checked(version + 1),
                Resolution = status == "Resolved" ? new(resolution!, c.ActorId, c.ClientId, now, version + 1) : i.Resolution,
                History = i.History.Add(new(version + 1, status, status == "Resolved" ? "resolved" : "updated", c.ActorId, c.ClientId, c.CorrelationId, now, null))
            };
            tx.Context.Incidents.Incidents[tx.Context.Incidents.Incidents.IndexOf(i)] = next;
            Guid? notification = null;
            if (status == "Resolved")
            {
                foreach (var n in tx.Notifications.Messages.Where(n => !n.Deleted && n.IncidentId == id && n.SourceKind == "vehicle-dispatch" && n.State is not ("Sent" or "Cancelled" or "Superseded")).ToArray())
                    tx.Notifications.Messages[tx.Notifications.Messages.IndexOf(n)] = n with
                    {
                        State = "Superseded",
                        Version = n.Version + 1,
                        History = n.History.Add(new(n.Version + 1, "Superseded", "incident-resolved", now, c.ActorId, c.ClientId, c.CorrelationId))
                    };
                notification = Draft(tx, c, next, "incident-resolution").Id;
            }
            var result = new MutationResult(id, next.Version); Remember(tx, c, scope, payload, "incidents.update", result);
            tx.Context.Incidents.Audit.Add(new(c.TenantId, c.ActorId, c.ClientId, c.CorrelationId, "incidents.update", id, next.Version, now));
            return new IncidentUpdateResult(result, notification);
        }, ct);
    }
    public Task<MutationResult> DeleteIncident(FleetCaller c, Guid id, long version, string key, CancellationToken ct)
    {
        Validate(c, version, key); var scope = Scope(c, "incidents.delete/" + id, key); var payload = JsonSerializer.Serialize(new { version });
        return store.Execute(c.TenantId, tx =>
        {
            if (Replay(tx, scope, payload) is { } prior) return prior;
            var i = Incident(tx, c, id); var now = clock.GetUtcNow(); if (i.Version != version) throw new IncidentException(409);
            var messages = tx.Notifications.Messages.Where(n => !n.Deleted && n.IncidentId == id).ToArray();
            foreach (var n in messages) Idle(n, now);
            foreach (var n in messages) tx.Notifications.Messages[tx.Notifications.Messages.IndexOf(n)] = n with
            {
                Deleted = true,
                Version = checked(n.Version + 1),
                History = n.History.Add(new(n.Version + 1, n.State, "incident-deleted", now, c.ActorId, c.ClientId, c.CorrelationId))
            };
            var next = i with { Deleted = true, Version = checked(version + 1), History = i.History.Add(new(version + 1, i.Status, "deleted", c.ActorId, c.ClientId, c.CorrelationId, now, null)) };
            tx.Context.Incidents.Incidents[tx.Context.Incidents.Incidents.IndexOf(i)] = next;
            var result = new MutationResult(id, next.Version); Remember(tx, c, scope, payload, "incidents.delete", result);
            tx.Context.Incidents.Audit.Add(new(c.TenantId, c.ActorId, c.ClientId, c.CorrelationId, "incidents.delete", id, next.Version, now)); return result;
        }, ct);
    }
}
