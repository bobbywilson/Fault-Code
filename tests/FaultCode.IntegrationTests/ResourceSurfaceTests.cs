using System.Net;
using System.Text.Json;
using FaultCode.Application.Incidents;
using FaultCode.Application.Notifications;
using FaultCode.Application.Telemetry;
using Microsoft.AspNetCore.Mvc.Testing;
using static FaultCode.IntegrationTests.EntraAuthenticationTests;
using static FaultCode.IntegrationTests.EngineeringTests;
namespace FaultCode.IntegrationTests;

public sealed class ResourceSurfaceTests
{
    [Fact]
    public async Task Generated_contract_contains_exactly_six_crud_resources_and_no_identity_management()
    {
        using var factory = new WebApplicationFactory<Program>(); using var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = doc.RootElement.GetProperty("paths"); Assert.Equal(12, paths.EnumerateObject().Count());
        foreach (var resource in new[] { "incidents", "vehicles", "drivers", "notifications", "fault-codes", "ontology" })
        {
            var list = paths.GetProperty("/api/v1/" + resource); var item = paths.GetProperty("/api/v1/" + resource + "/{id}");
            Assert.Equal(new[] { "get", "post" }, list.EnumerateObject().Select(p => p.Name).Order().ToArray());
            Assert.Equal(new[] { "delete", "get", "put" }, item.EnumerateObject().Select(p => p.Name).Order().ToArray());
            foreach (var operation in list.EnumerateObject().Concat(item.EnumerateObject())) Assert.True(operation.Value.TryGetProperty("security", out _));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/" + resource)).StatusCode);
        }
        foreach (var old in new[] { "account", "admin/users", "admin/roles", "faults", "fault-code-catalog", "ontology/relationships", "authorization/me", "vehicle-fault-dispatches", "mcp" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/" + old)).StatusCode);
    }
    private static async Task<(TestEngineeringStore Store, FaultDispatchReceipt Receipt)> Fixture()
    {
        var store = new TestEngineeringStore(); var vehicle = Guid.NewGuid(); var driver = Guid.NewGuid();
        await store.NotificationExecute(Tenant, tx =>
        {
            tx.Fleet.Vehicles.Add(new(vehicle, Tenant, "SYN-SILVERADO-001", "EPS", true, 1));
            tx.Fleet.Drivers.Add(new(driver, Tenant, "DRV-DEMO", Actor, "Alex Morgan", "alex.morgan@example.com", false, true, 1));
            tx.Fleet.Associations.Add(new(Guid.NewGuid(), vehicle, driver, DateTimeOffset.UtcNow.AddHours(-1), null)); return 0;
        });
        var receipt = await new VehicleFaultDispatchService(store, TimeProvider.System, NullTelemetry.Instance).Dispatch(Caller, vehicle, "SYN-EPS:001", "intake", default);
        return (store, receipt);
    }
    [Fact]
    public async Task Notification_mutations_preserve_history_replay_versions_and_sent_receipts()
    {
        var (store, receipt) = await Fixture(); var service = new ResourceMutationService(store, TimeProvider.System);
        var cancelled = await service.UpdateNotification(Caller, receipt.NotificationId, "Cancelled", 1, "cancel", default);
        Assert.Equal(cancelled, await service.UpdateNotification(Caller, receipt.NotificationId, "Cancelled", 1, "cancel", default));
        Assert.Equal(409, (await Assert.ThrowsAsync<IncidentException>(() => service.UpdateNotification(Caller, receipt.NotificationId, "AwaitingAgent", 1, "stale", default))).Status);
        var retry = await service.UpdateNotification(Caller, receipt.NotificationId, "AwaitingAgent", cancelled.Version, "retry", default);
        var deleted = await service.DeleteNotification(Caller, receipt.NotificationId, retry.Version, "delete", default);
        Assert.Equal(deleted, await service.DeleteNotification(Caller, receipt.NotificationId, retry.Version, "delete", default));
        var n = Assert.Single(store.NotificationSnapshot().Notifications.Messages); Assert.True(n.Deleted); Assert.Empty(n.Attempts); Assert.Equal(4, n.History.Length);
        var notifications = new NotificationService(store, new NeverSend(), TimeProvider.System);
        Assert.Equal(404, (await Assert.ThrowsAsync<IncidentException>(() => notifications.Get(Caller, n.Id, default))).Status);
        var recreated = await service.CreateNotification(Caller, receipt.IncidentId, "new", default);
        Assert.NotEqual(n.Id, recreated.Id);
        Assert.Equal(409, (await Assert.ThrowsAsync<IncidentException>(() => service.CreateNotification(Caller, receipt.IncidentId, "duplicate", default))).Status);
    }
    [Fact]
    public async Task Incident_delete_hides_linked_records_and_retains_audit_without_sending()
    {
        var (store, receipt) = await Fixture(); var service = new ResourceMutationService(store, TimeProvider.System);
        var result = await service.DeleteIncident(Caller, receipt.IncidentId, 1, "delete", default);
        Assert.Equal(result, await service.DeleteIncident(Caller, receipt.IncidentId, 1, "delete", default));
        Assert.True(Assert.Single(store.NotificationSnapshot().Context.Incidents.Incidents).Deleted);
        Assert.True(Assert.Single(store.NotificationSnapshot().Notifications.Messages).Deleted);
        var incidents = new IncidentService(store, TimeProvider.System);
        Assert.Empty((await incidents.List(Caller, 1, 20, null, null, default)).Items);
        Assert.Equal(404, (await Assert.ThrowsAsync<IncidentException>(() => incidents.Get(Caller, receipt.IncidentId, default))).Status);
    }
    [Fact]
    public async Task Resolution_requires_notes_and_cross_owner_is_hidden()
    {
        var (store, receipt) = await Fixture(); var service = new ResourceMutationService(store, TimeProvider.System);
        Assert.Equal(400, (await Assert.ThrowsAsync<IncidentException>(() => service.UpdateIncident(Caller, receipt.IncidentId, "Resolved", 1, "resolve", default))).Status);
        var changed = await service.UpdateIncident(Caller, receipt.IncidentId, "Investigating", 1, "investigate", default);
        Assert.Equal(2, changed.Mutation.Version); Assert.Null(changed.NotificationId);
        var own = Caller with { ActorId = Client, AllDrivers = false, AllVehicles = false, OwnDrivers = true, OwnVehicles = true };
        Assert.Equal(404, (await Assert.ThrowsAsync<IncidentException>(() => service.DeleteIncident(own, receipt.IncidentId, 2, "denied", default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<IncidentException>(() => service.DeleteNotification(Caller with { TenantId = Client }, receipt.NotificationId, 1, "tenant", default))).Status);
    }
    [Fact]
    public async Task Active_delivery_blocks_destructive_mutations()
    {
        var (store, receipt) = await Fixture(); var service = new ResourceMutationService(store, TimeProvider.System);
        await store.NotificationExecute(Tenant, tx => { var n = tx.Notifications.Messages[0]; tx.Notifications.Messages[0] = n with { State = "Sending", LeaseId = Guid.NewGuid(), LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(1) }; return 0; });
        Assert.Equal(409, (await Assert.ThrowsAsync<IncidentException>(() => service.DeleteIncident(Caller, receipt.IncidentId, 1, "delete", default))).Status);
        Assert.Equal(409, (await Assert.ThrowsAsync<IncidentException>(() => service.DeleteNotification(Caller, receipt.NotificationId, 1, "delete", default))).Status);
    }
    private sealed class NeverSend : INotificationDelivery
    { public Task<DeliveryAcknowledgement> Send(LocalDelivery message, CancellationToken ct) => throw new InvalidOperationException("CRUD must not call the provider in a transaction."); }
}
