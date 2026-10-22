using FaultCode.Application.Incidents;
using FaultCode.Application.Notifications;
using FaultCode.Application.Telemetry;
using FaultCode.Domain;
using static FaultCode.IntegrationTests.EntraAuthenticationTests;
using static FaultCode.IntegrationTests.EngineeringTests;

namespace FaultCode.IntegrationTests;

public sealed class VehicleFaultDispatchTests
{
    private static readonly Guid Vehicle = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Driver = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static async Task<TestEngineeringStore> Store()
    {
        var store = new TestEngineeringStore();
        await store.NotificationExecute(Tenant, tx =>
        {
            tx.Fleet.Vehicles.Add(new(Vehicle, Tenant, "SYN-SILVERADO-001", "EPS", true, 1));
            tx.Fleet.Drivers.Add(new(Driver, Tenant, "DRV-DEMO", Actor, "Alex Morgan", "alex.morgan@example.com", false, true, 1));
            tx.Fleet.Associations.Add(new(Guid.NewGuid(), Vehicle, Driver, DateTimeOffset.UtcNow.AddHours(-1), null));
            return 0;
        });
        return store;
    }
    private static VehicleFaultDispatchService Service(TestEngineeringStore store) => new(store, TimeProvider.System, NullTelemetry.Instance);

    [Fact]
    public async Task Two_field_dispatch_atomically_creates_linked_incident_notification_and_work()
    {
        var store = await Store();
        var result = await Service(store).Dispatch(Caller, Vehicle, "syn-eps:001", "dispatch-one", default);
        var state = store.NotificationSnapshot();
        var incident = Assert.Single(state.Context.Incidents.Incidents);
        var notification = Assert.Single(state.Notifications.Messages);
        Assert.Equal(result.IncidentId, incident.Id);
        Assert.Equal(result.NotificationId, notification.Id);
        Assert.Equal(incident.Id, notification.IncidentId);
        Assert.Equal(Driver, notification.DriverId);
        Assert.Equal(Vehicle, incident.Fleet.VehicleId);
        Assert.Equal("SYN-EPS:001", incident.Code);
        Assert.Equal("AwaitingAgent", notification.State);
        Assert.Empty(notification.Message);
        Assert.Empty(notification.Attempts);
        Assert.Null(incident.Knowledge);
        Assert.Equal("Pending", Assert.Single(incident.Work).State);
        Assert.Equal("Accepted", result.State);
    }

    [Fact]
    public async Task Stable_replay_reuses_both_ids_and_changed_code_conflicts()
    {
        var store = await Store(); var service = Service(store);
        var first = await service.Dispatch(Caller, Vehicle, "SYN-EPS:001", "same", default);
        Assert.Equal(first, await service.Dispatch(Caller, Vehicle, " syn-eps:001 ", "same", default));
        var error = await Assert.ThrowsAsync<IncidentException>(() => service.Dispatch(Caller, Vehicle, "SYN-EPS:002", "same", default));
        Assert.Equal(409, error.Status);
        Assert.Single(store.NotificationSnapshot().Context.Incidents.Incidents);
        Assert.Single(store.NotificationSnapshot().Notifications.Messages);
    }

    [Fact]
    public async Task Commit_failure_leaves_neither_record_and_cross_owner_is_hidden()
    {
        var store = await Store(); var service = Service(store);
        var denied = await Assert.ThrowsAsync<IncidentException>(() => service.Dispatch(Caller with { ActorId = Client, AllDrivers = false, OwnDrivers = true, AllVehicles = false, OwnVehicles = true }, Vehicle, "SYN-EPS:001", "denied", default));
        Assert.Equal(404, denied.Status);
        store.FailCommit = true;
        await Assert.ThrowsAnyAsync<Exception>(() => service.Dispatch(Caller, Vehicle, "SYN-EPS:001", "rollback", default));
        Assert.Empty(store.NotificationSnapshot().Context.Incidents.Incidents);
        Assert.Empty(store.NotificationSnapshot().Notifications.Messages);
    }

    [Fact]
    public async Task Missing_or_ambiguous_associations_and_invalid_codes_cannot_create_records()
    {
        var store = await Store();
        await store.NotificationExecute(Tenant, tx => { tx.Fleet.Associations.Clear(); return 0; });
        Assert.Equal(409, (await Assert.ThrowsAsync<IncidentException>(() => Service(store).Dispatch(Caller, Vehicle, "SYN-EPS:001", "missing", default))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<IncidentException>(() => Service(store).Dispatch(Caller, Vehicle, "not-a-code", "invalid", default))).Status);
        Assert.Empty(store.NotificationSnapshot().Context.Incidents.Incidents);
        Assert.Empty(store.NotificationSnapshot().Notifications.Messages);
    }
}
