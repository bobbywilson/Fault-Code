using System.Security.Claims;
using System.Text.Json;
using FaultCode.Api.Mcp;
using FaultCode.Application.Agents;
using FaultCode.Application.Incidents;
using FaultCode.Application.Knowledge;
using FaultCode.Application.Notifications;
using FaultCode.Application.Telemetry;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static FaultCode.IntegrationTests.EntraAuthenticationTests;
using static FaultCode.IntegrationTests.EngineeringTests;

namespace FaultCode.IntegrationTests;

public sealed class IncidentResolutionTests
{
    private const string Notes = "Connector inspection completed.\nArrange a qualified service assessment before further use.";
    private static async Task<(TestEngineeringStore Store, FaultDispatchReceipt Receipt)> Fixture()
    {
        var store = new TestEngineeringStore(); var vehicle = Guid.NewGuid(); var driver = Guid.NewGuid();
        await store.NotificationExecute(Tenant, tx =>
        {
            tx.Fleet.Vehicles.Add(new(vehicle, Tenant, "Silverado", "EPS", true, 1));
            tx.Fleet.Drivers.Add(new(driver, Tenant, "DRV-RESOLVE", Actor, "Alex Morgan", "alex.morgan@example.com", false, true, 1));
            tx.Fleet.Associations.Add(new(Guid.NewGuid(), vehicle, driver, DateTimeOffset.UtcNow.AddHours(-1), null)); return 0;
        });
        return (store, await new VehicleFaultDispatchService(store, TimeProvider.System, NullTelemetry.Instance).Dispatch(Caller, vehicle, "SYN-EPS:UNLISTED", "intake", default));
    }
    [Fact]
    public async Task Unknown_resolution_is_audited_and_replay_cannot_change_notes_or_duplicate_notifications()
    {
        var (store, receipt) = await Fixture(); var service = new ResourceMutationService(store, TimeProvider.System);
        foreach (var notes in new[] { "", "   ", new string('x', 2001), "control\0" })
            Assert.Equal(400, (await Assert.ThrowsAsync<IncidentException>(() => service.UpdateIncident(Caller, receipt.IncidentId, "Resolved", 1, "invalid", default, notes))).Status);
        var result = await service.UpdateIncident(Caller, receipt.IncidentId, "Resolved", 1, "resolve", default, Notes);
        Assert.NotNull(result.NotificationId);
        Assert.Equal(result, await service.UpdateIncident(Caller, receipt.IncidentId, "Resolved", 1, "resolve", default, Notes));
        var snapshot = store.NotificationSnapshot(); var incident = Assert.Single(snapshot.Context.Incidents.Incidents);
        Assert.Equal("Resolved", incident.Status); Assert.Equal(Notes, incident.Resolution!.Text); Assert.Equal(Actor, incident.Resolution.ActorId); Assert.Equal(2, incident.Resolution.Version);
        Assert.Equal("resolved", incident.History[^1].Action); Assert.Equal(2, snapshot.Notifications.Messages.Count);
        Assert.Equal("Superseded", snapshot.Notifications.Messages.Single(n => n.Id == receipt.NotificationId).State);
        Assert.Equal(409, (await Assert.ThrowsAsync<IncidentException>(() => service.UpdateIncident(Caller, receipt.IncidentId, "Resolved", 1, "resolve", default, "Different notes"))).Status);
        Assert.Equal(409, (await Assert.ThrowsAsync<IncidentException>(() => service.UpdateIncident(Caller, receipt.IncidentId, "Resolved", 1, "stale", default, Notes))).Status);
        Assert.Equal(2, store.NotificationSnapshot().Notifications.Messages.Count);
    }
    [Theory]
    [InlineData("success")]
    [InlineData("index-failure")]
    [InlineData("wrong-vector-evidence")]
    public async Task Resolution_flows_through_vector_evidence_and_authorized_MCP_before_followup(string failure)
    {
        var (store, receipt) = await Fixture(); var change = await new ResourceMutationService(store, TimeProvider.System).UpdateIncident(Caller, receipt.IncidentId, "Resolved", 1, "resolve", default, Notes);
        var index = new ResolutionIndex { Fail = failure == "index-failure", Corrupt = failure == "wrong-vector-evidence" };
        var delivery = new Capture(); var agent = new ToolAgent(); using var keys = new Discovery();
        await using var factory = CreateFactory(keys).WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.Replace(ServiceDescriptor.Singleton<INotificationStore>(store)); s.Replace(ServiceDescriptor.Singleton<IKnowledgeStore>(store));
            s.Replace(ServiceDescriptor.Singleton<IIncidentResolutionIndex>(index)); s.Replace(ServiceDescriptor.Singleton<INotificationDelivery>(delivery));
            s.Replace(ServiceDescriptor.Singleton<IFaultEmailAgent>(agent));
        }));
        using var http = factory.CreateClient(); using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            TraceIdentifier = "resolution-test",
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                new Claim("tid",Tenant),new Claim("oid",Actor),new Claim("azp",Client),new Claim("roles","Operator"),
                new Claim("scp","faults.read faults.write knowledge.read vehicles.read drivers.read")}, "fixture"))
        };
        var workflow = scope.ServiceProvider.GetRequiredService<FaultEmailWorkflow>(); var notification = change.NotificationId!.Value;
        if (failure != "success")
        {
            await Assert.ThrowsAnyAsync<Exception>(() => workflow.Process(Caller, notification, default));
            Assert.Empty(delivery.Sends); Assert.Equal("AgentFailed", store.NotificationSnapshot().Notifications.Messages.Single(n => n.Id == notification).State);
            Assert.Equal(Notes, store.NotificationSnapshot().Context.Incidents.Incidents.Single().Resolution!.Text);
            index.Fail = false; index.Corrupt = false;
            await store.NotificationExecute(Tenant, tx => { var n = tx.Notifications.Messages.Single(n => n.Id == notification); tx.Notifications.Messages[tx.Notifications.Messages.IndexOf(n)] = n with { NextAttemptAt = null }; return 0; });
        }
        await workflow.Process(Caller, notification, default);
        Assert.Equal(Notes, index.Document!.Resolution); Assert.Equal("SYN-EPS:UNLISTED", index.Document.Code);
        var sent = Assert.Single(delivery.Sends); Assert.Contains(Notes, sent.Message); Assert.DoesNotContain("We are investigating", sent.Message);
        var record = store.NotificationSnapshot().Notifications.Messages.Single(n => n.Id == notification); Assert.Equal("Sent", record.State); Assert.Single(record.Attempts);
        await workflow.Process(Caller, notification, default); Assert.Single(delivery.Sends);
    }
    private sealed class ResolutionIndex : IIncidentResolutionIndex
    {
        public bool Fail; public bool Corrupt; public IncidentResolutionDocument? Document;
        public Task UpsertResolution(string tenant, IncidentResolutionDocument document, CancellationToken ct) { Assert.Equal(Tenant, tenant); if (Fail) throw new KnowledgeException(503); Document = document; return Task.CompletedTask; }
        public Task<IncidentResolutionDocument?> FindResolution(string tenant, Guid id, CancellationToken ct) { Assert.Equal(Tenant, tenant); Assert.Equal(Document!.IncidentId, id); return Task.FromResult<IncidentResolutionDocument?>(Corrupt ? Document with { Resolution = "Untrusted changed resolution" } : Document); }
    }
    private sealed class ToolAgent : IFaultEmailAgent
    {
        public async Task Run(FaultEmailTool tool, CancellationToken ct)
        {
            using var lookup = JsonDocument.Parse(await tool("notification_fault_lookup", [], ct));
            if (lookup.RootElement.GetProperty("status").GetInt32() != 200) throw new AgentFailure("lookup-failed");
            var facts = lookup.RootElement.GetProperty("data");
            using var result = JsonDocument.Parse(await tool("notification_email_send", new() { ["summary"] = facts.GetProperty("summary").GetString()!, ["action"] = facts.GetProperty("action").GetString()!, ["tone"] = "friendly" }, ct));
            Assert.Equal(200, result.RootElement.GetProperty("status").GetInt32());
        }
    }
    private sealed class Capture : INotificationDelivery
    {
        public List<LocalDelivery> Sends = [];
        public Task<DeliveryAcknowledgement> Send(LocalDelivery message, CancellationToken ct) { Sends.Add(message); return Task.FromResult(new DeliveryAcknowledgement("resolution-receipt")); }
    }
}
