using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FaultCode.Application.Fleet;
using FaultCode.Application.Notifications;
using FaultCode.Application.Telemetry;
using FaultCode.Domain;

namespace FaultCode.Application.Incidents;

public sealed record FaultDispatchReceipt(Guid IncidentId, Guid NotificationId, string State);

// Intake commits intent only. Neither a saved draft nor queued work proves agent execution or delivery.
public sealed class VehicleFaultDispatchService(INotificationStore store, TimeProvider clock, ITelemetry telemetry)
{
    public Task<FaultDispatchReceipt> Dispatch(FleetCaller caller, Guid vehicleId, string code, string key, CancellationToken ct) =>
        telemetry.Run(TelemetryOperation.Incident, caller.CorrelationId, () => Accept(caller, vehicleId, code, key, ct));

    private Task<FaultDispatchReceipt> Accept(FleetCaller c, Guid vehicleId, string code, string key, CancellationToken ct)
    {
        if (!FleetRules.CanonicalId(c.TenantId) || !FleetRules.CanonicalId(c.ActorId) || !FleetRules.CanonicalId(c.ClientId)
            || string.IsNullOrWhiteSpace(c.CorrelationId)) throw new IncidentException(403);
        if (vehicleId == Guid.Empty || !IncidentRules.Token(key) || !FaultRules.Text(code, 60)) throw new IncidentException(400);
        var replayKey = JsonSerializer.Serialize(new[] { c.TenantId, c.ActorId, c.ClientId, "vehicle-fault-dispatch", key });
        return store.Execute(c.TenantId, tx =>
        {
            var vehicle = tx.Fleet.Vehicles.SingleOrDefault(v => v.Id == vehicleId && v.TenantId == c.TenantId) ?? throw new IncidentException(404);
            string normalized;
            try { normalized = FaultRules.Code(code, vehicle.System); }
            catch (ArgumentException) { throw new IncidentException(400); }
            var payload = JsonSerializer.Serialize(new { vehicleId, code = normalized });
            if (tx.Notifications.Replays.TryGetValue(replayKey, out var replay))
            {
                if (replay.Payload != payload) throw new IncidentException(409);
                var prior = tx.Notifications.Messages.Single(n => n.Id == replay.Result.Id && n.TenantId == c.TenantId);
                return new FaultDispatchReceipt(prior.IncidentId, prior.Id, "Accepted");
            }
            var associations = tx.Fleet.Associations.Where(a => a.VehicleId == vehicleId && a.Until is null).ToArray();
            if (!vehicle.Active || associations.Length != 1) throw new IncidentException(409);
            var association = associations[0];
            var driver = tx.Fleet.Drivers.SingleOrDefault(d => d.Id == association.DriverId && d.TenantId == c.TenantId) ?? throw new IncidentException(404);
            if (!FleetService.CanRead(c, driver, true) || !FleetService.CanRead(c, driver)) throw new IncidentException(404);
            if (!driver.Active || driver.Email is null || !NotificationRules.Contact(driver.Email)) throw new IncidentException(409);
            // These are explicitly seeded demo metadata, not inferred real OEM software configurations.
            if (!Demo.DemoFleetFactory.Create().Vehicles.Any(v => v.Identifier == vehicle.Identifier)) throw new IncidentException(409);
            const string model = "SYN-STEERING-DEMO", software = "demo-1";
            var now = clock.GetUtcNow();
            if (association.From > now) throw new IncidentException(409);
            var incidentId = Guid.NewGuid(); var notificationId = Guid.NewGuid();
            var source = "dispatch-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(replayKey)))[..40];
            var fleet = new IncidentFleet(vehicle.Id, vehicle.Identifier, vehicle.Version, driver.Id, driver.Identifier,
                driver.Version, driver.OwnerObjectId, association);
            tx.Context.Incidents.Incidents.Add(new(incidentId, c.TenantId, c.ClientId, source, normalized, vehicle.System,
                model, software, now, now, c.CorrelationId, fleet, null, "Received", 1,
                [new(1, "Received", "vehicle-fault-dispatched", c.ActorId, c.ClientId, c.CorrelationId, now, null)],
                [new(Guid.NewGuid(), "fault-lookup-and-email", "Pending", 0, null)]));
            tx.Context.Incidents.Audit.Add(new(c.TenantId, c.ActorId, c.ClientId, c.CorrelationId, "vehicle-fault-dispatch", incidentId, 1, now));
            // No diagnosis, invented approval or email body is assigned before actual agent/tool processing.
            tx.Notifications.Messages.Add(new(notificationId, c.TenantId, incidentId, driver.Id, driver.OwnerObjectId,
                "vehicle-dispatch", incidentId, Guid.Empty, 0, 0,
                new("awaiting-investigation", "guidance", "Awaiting investigation", false, model, software, null), [],
                "vehicle-dispatch-v1", "", driver.Email, driver.Version, 1, "AwaitingAgent", 1, now, null, null, null, [],
                [new(1, "AwaitingAgent", "created-with-incident", now, c.ActorId, c.ClientId, c.CorrelationId)]));
            tx.Notifications.Replays.Add(replayKey, new(payload, new(notificationId, 1)));
            tx.Notifications.Audit.Add(new(c.ActorId, c.ClientId, c.CorrelationId, "vehicle-fault-dispatch", notificationId, 1, now));
            return new FaultDispatchReceipt(incidentId, notificationId, "Accepted");
        }, ct);
    }
}
