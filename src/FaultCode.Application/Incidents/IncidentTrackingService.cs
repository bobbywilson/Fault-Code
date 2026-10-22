using FaultCode.Application.Notifications;
using FaultCode.Domain;

namespace FaultCode.Application.Incidents;

public sealed record TrackedIncident(string Id, long Number, string Url);
public interface IIncidentTracker
{
    Task<TrackedIncident> Publish(Incident incident, string? externalId, CancellationToken ct);
}
public sealed class IncidentTrackingException(string code, bool uncertain = false) : Exception
{
    public string Code { get; } = code;
    public bool Uncertain { get; } = uncertain;
}

// Provider projection has its own durable lease/version; it never changes the
// business ETag or supplies a guessed provider ID after an ambiguous create.
public sealed class IncidentTrackingService(INotificationStore store, IIncidentTracker tracker, TimeProvider clock)
{
    public async Task Sync(string tenant, Guid id, CancellationToken ct)
    {
        var claim = await store.Execute<Incident?>(tenant, tx =>
        {
            var i = tx.Context.Incidents.Incidents.SingleOrDefault(i => i.Id == id && i.TenantId == tenant && !i.Deleted);
            if (i is null) return null;
            var prior = i.Tracking; var now = clock.GetUtcNow();
            if (prior?.State == "Linked" && prior.Version >= i.Version || prior?.State == "Unknown") return null;
            if (prior?.State == "Publishing")
            {
                if (prior.LeaseUntil > now) return null;
                if (prior.Id is null)
                {
                    tx.Context.Incidents.Incidents[tx.Context.Incidents.Incidents.IndexOf(i)] = i with
                    {
                        Tracking = prior with { State = "Unknown", Lease = null, LeaseUntil = null, FailureCode = "provider-acceptance-unknown" }
                    };
                    return null;
                }
            }
            var next = i with
            {
                Tracking = new("Publishing", prior?.Id, prior?.Number, prior?.Url, prior?.Version ?? 0,
                Guid.NewGuid(), now.AddSeconds(30), null)
            };
            tx.Context.Incidents.Incidents[tx.Context.Incidents.Incidents.IndexOf(i)] = next; return next;
        }, ct);
        if (claim is null) return;
        TrackedIncident? receipt = null; string? failure = null; var uncertain = false;
        try { receipt = await tracker.Publish(claim, claim.Tracking!.Id, ct); }
        catch (IncidentTrackingException e) { failure = e.Code; uncertain = e.Uncertain; }
        catch (Exception) { failure = "provider-acceptance-unknown"; uncertain = true; }
        await store.Execute(tenant, tx =>
        {
            var current = tx.Context.Incidents.Incidents.Single(i => i.Id == id);
            if (current.Tracking is not { } prior || prior.Lease != claim.Tracking!.Lease) return 0;
            var next = receipt is null
                ? prior with { State = uncertain && prior.Id is null ? "Unknown" : "Failed", Lease = null, LeaseUntil = null, FailureCode = failure }
                : new IncidentTracking("Linked", receipt.Id, receipt.Number, receipt.Url, claim.Version, null, null, null);
            tx.Context.Incidents.Incidents[tx.Context.Incidents.Incidents.IndexOf(current)] = current with { Tracking = next };
            return 0;
        }, CancellationToken.None);
    }
}
