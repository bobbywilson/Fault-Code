using FaultCode.Application.Agents;
using FaultCode.Application.Fleet;
using FaultCode.Application.Incidents;
using FaultCode.Application.Knowledge;
using FaultCode.Application.Telemetry;
using FaultCode.Domain;
namespace FaultCode.Application.Notifications;
// Model tools cannot supply recipients, credentials, ownership, approvals or arbitrary diagnostics.
public sealed class FaultEmailService(INotificationStore store, KnowledgeService knowledge, IFaultCodeIndex index, IIncidentResolutionIndex resolutions,
    INotificationDelivery delivery, TimeProvider clock, ITelemetry telemetry)
{
    private static bool Visible(Incident i, FleetCaller c) => !i.Deleted && i.TenantId == c.TenantId &&
        (c.AllVehicles || c.OwnVehicles && i.Fleet.OwnerObjectId == c.ActorId) && (c.AllDrivers || c.OwnDrivers && i.Fleet.OwnerObjectId == c.ActorId);
    private static DriverNotification Find(NotificationTransaction tx, FleetCaller c, Guid id)
    {
        if (!FleetRules.CanonicalId(c.TenantId) || !FleetRules.CanonicalId(c.ActorId) || !FleetRules.CanonicalId(c.ClientId)) throw new IncidentException(403);
        var n = tx.Notifications.Messages.SingleOrDefault(n => n.Id == id && n.TenantId == c.TenantId && !n.Deleted) ?? throw new IncidentException(404);
        var i = tx.Context.Incidents.Incidents.SingleOrDefault(i => i.Id == n.IncidentId && Visible(i, c)) ?? throw new IncidentException(404);
        var d = tx.Fleet.Drivers.SingleOrDefault(d => d.Id == n.DriverId && d.TenantId == c.TenantId);
        if (n.SourceKind is not ("vehicle-dispatch" or "incident-resolution") || d is null || !d.Active || d.Email != n.Contact
            || !n.Contact.EndsWith("@example.com", StringComparison.Ordinal) || i.Fleet.DriverId != d.Id) throw new IncidentException(409);
        return n;
    }
    private void Save(NotificationTransaction tx, FleetCaller c, DriverNotification old, DriverNotification next, string action)
    {
        next = next with { Version = checked(old.Version + 1), History = next.History.Add(new(checked(old.Version + 1), next.State, action, clock.GetUtcNow(), c.ActorId, c.ClientId, c.CorrelationId)) };
        tx.Notifications.Messages[tx.Notifications.Messages.IndexOf(old)] = next;
        tx.Notifications.Audit.Add(new(c.ActorId, c.ClientId, c.CorrelationId, action, next.Id, next.Version, clock.GetUtcNow()));
    }
    private void Lease(DriverNotification n, Guid lease)
    { if (n.LeaseId != lease || n.LeaseUntil <= clock.GetUtcNow()) throw new IncidentException(409); }
    public Task<Guid?> Acquire(FleetCaller c, Guid id, CancellationToken ct) => store.Execute<Guid?>(c.TenantId, tx =>
    {
        var n = Find(tx, c, id); var now = clock.GetUtcNow();
        if (n.State == "Sent") return null;
        if (n.State is "Cancelled" or "Superseded" or "Sending" || n.Attempts.LastOrDefault()?.FailureCode == "acceptance-unknown") throw new IncidentException(409);
        if (n.LeaseUntil > now || n.NextAttemptAt > now || n.Attempts.Length >= 5) throw new IncidentException(409);
        var lease = Guid.NewGuid(); Save(tx, c, n, n with { State = "Processing", LeaseId = lease, LeaseUntil = now.AddMinutes(2), NextAttemptAt = null }, "agent-started"); return lease;
    }, ct);
    private static IncidentResolutionDocument Evidence(Incident i) => i.Resolution is { } r
        ? new(i.Id, i.Code, i.System, i.Model, i.SoftwareVersion, r.Version, r.Text) : throw new IncidentException(409);
    public async Task Prepare(FleetCaller c, Guid id, Guid lease, CancellationToken ct)
    {
        var document = await store.Execute(c.TenantId, tx =>
        {
            var n = Find(tx, c, id); Lease(n, lease);
            return n.SourceKind == "incident-resolution" ? Evidence(tx.Context.Incidents.Incidents.Single(i => i.Id == n.IncidentId)) : null;
        }, ct);
        if (document is not null) await resolutions.UpsertResolution(c.TenantId, document, ct);
    }
    public async Task<FaultEmailFacts> Lookup(FleetCaller c, Guid id, Guid lease, CancellationToken ct)
    {
        var context = await store.Execute(c.TenantId, tx => { var n = Find(tx, c, id); Lease(n, lease); return new { Incident = tx.Context.Incidents.Incidents.Single(i => i.Id == n.IncidentId), Resolution = n.SourceKind == "incident-resolution" }; }, ct);
        var incident = context.Incident;
        if (context.Resolution)
        {
            var expected = Evidence(incident);
            var found = await resolutions.FindResolution(c.TenantId, incident.Id, ct);
            if (found != expected) throw new KnowledgeException(503, "resolution-index-stale");
            return new(incident.Code, $"The investigation of fault code {incident.Code} has been resolved. Recorded resolution: {found.Resolution}",
                "These are the recorded investigation findings. Resolution of the investigation does not confirm vehicle repairs or authorize a software installation. Contact a qualified service technician if further assessment is needed.", null, 0);
        }
        ApprovedFault? fault = null;
        try { fault = await knowledge.Lookup(new(c.TenantId, c.ActorId, c.ClientId, c.CorrelationId), incident.Code, incident.System, incident.Model, incident.SoftwareVersion, ct); }
        catch (KnowledgeException e) when (e.Status == 404) { }
        var hit = await index.Find(c.TenantId, incident.Code, incident.System, incident.Model, incident.SoftwareVersion, ct);
        if ((fault is null) != (hit is null) || fault is not null && (hit!.Id != fault.Id || hit.Version != fault.Version)) throw new KnowledgeException(503, "fault-index-stale");
        return fault is null
            ? new(incident.Code, $"Your vehicle reported fault code {incident.Code}. We are investigating and do not yet have a verified diagnosis.", "We will send a follow-up when the investigation is resolved. Arrange a qualified service assessment if you have concerns about steering operation.", null, 0)
            : new(incident.Code, $"Your vehicle reported fault code {incident.Code}. {fault.Content.Description}", fault.Content.Remedies.Length > 0 ? string.Join(" ", fault.Content.Remedies.Select(r => r.Description)) : "Arrange an assessment by a qualified service technician. No approved repair or software patch is provided for this demonstration fault.", fault.Id, fault.Version);
    }
    public async Task<string> Send(FleetCaller c, Guid id, Guid lease, string summary, string action, string tone, CancellationToken ct)
    {
        var facts = await Lookup(c, id, lease, ct);
        if (summary != facts.Summary || action != facts.Action || tone is not ("friendly" or "formal")) throw new IncidentException(422);
        var n = await store.Execute(c.TenantId, tx =>
        {
            var old = Find(tx, c, id); Lease(old, lease);
            if (old.State != "Processing") throw new IncidentException(409);
            var d = tx.Fleet.Drivers.Single(d => d.Id == old.DriverId && d.TenantId == c.TenantId);
            var text = (tone == "friendly" ? $"Hi {d.DisplayName}," : $"Dear {d.DisplayName},") + "\n\n" + summary + "\n\n" + action + "\n\n" + (tone == "friendly" ? "Thank you,\nFault Code" : "Regards,\nFault Code");
            var next = old with
            {
                State = "Sending",
                Message = text,
                FaultId = facts.FaultId ?? Guid.Empty,
                PublicationVersion = facts.PublicationVersion,
                Attempts = old.Attempts.Add(new(Guid.NewGuid(), old.Attempts.Length + 1, clock.GetUtcNow(), null, "Sending", null, null))
            };
            Save(tx, c, old, next, "delivery-started"); return next;
        }, ct);
        string? receipt = null; string? failure = null;
        using var operation = telemetry.Begin(TelemetryOperation.Delivery, c.CorrelationId);
        try { receipt = (await delivery.Send(new(id, n.Contact, n.Message), ct)).Receipt; if (!FaultRules.Text(receipt, 100)) { receipt = null; failure = "acceptance-unknown"; } }
        catch { failure = "acceptance-unknown"; }
        // Finalize durable attempt even after the request deadline. Unknown acceptance is never blindly resent.
        await store.Execute(c.TenantId, tx =>
        {
            var old = Find(tx, c, id); if (old.LeaseId != lease) throw new IncidentException(409);
            var next = old with
            {
                State = receipt is null ? "DeliveryUnknown" : "Sent",
                LeaseId = null,
                LeaseUntil = null,
                Attempts = old.Attempts.SetItem(old.Attempts.Length - 1, old.Attempts[^1] with { CompletedAt = clock.GetUtcNow(), State = receipt is null ? "Unknown" : "Sent", FailureCode = failure, Acknowledgement = receipt })
            };
            Save(tx, c, old, next, receipt is null ? "delivery-uncertain" : "mailpit-accepted"); return 0;
        }, CancellationToken.None);
        operation.Complete(receipt is null ? TelemetryOutcome.Failed : TelemetryOutcome.Success);
        if (receipt is null) throw new IncidentException(503);
        return "Mailpit accepted the email; delivery receipt recorded.";
    }
    public Task Fail(FleetCaller c, Guid id, Guid lease, CancellationToken ct) => store.Execute(c.TenantId, tx =>
    {
        var n = Find(tx, c, id);
        if (n.LeaseId == lease && n.State == "Processing") Save(tx, c, n, n with { State = "AgentFailed", LeaseId = null, LeaseUntil = null, NextAttemptAt = clock.GetUtcNow().AddSeconds(30) }, "agent-failed");
        return 0;
    }, ct);
}
