using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FaultCode.Application.Fleet;
using FaultCode.Application.Incidents;
using FaultCode.Application.Knowledge;
using FaultCode.Application.Notifications;
using FaultCode.Application.Ontology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static FaultCode.IntegrationTests.EntraAuthenticationTests;
using static FaultCode.IntegrationTests.EngineeringTests;
namespace FaultCode.IntegrationTests;

public sealed class ResourceCrudHttpTests
{
    private static async Task<JsonElement> Send(HttpClient client, HttpMethod method, string path, object? body = null, string? version = null, int status = 200)
    {
        using var request = new HttpRequestMessage(method, path); if (body is not null) request.Content = JsonContent.Create(body);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N")); if (version is not null) request.Headers.TryAddWithoutValidation("If-Match", version);
        using var response = await client.SendAsync(request); Assert.Equal((HttpStatusCode)status, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }
    [Fact]
    public async Task Six_resource_CRUD_runs_through_real_JWT_middleware_and_registered_application_services()
    {
        var store = new TestEngineeringStore(); var vehicle = Guid.NewGuid(); var driver = Guid.NewGuid();
        await store.NotificationExecute(Tenant, tx => { tx.Fleet.Vehicles.Add(new(vehicle, Tenant, "SYN-SILVERADO-001", "EPS", true, 1)); tx.Fleet.Drivers.Add(new(driver, Tenant, "DRV-SEED", Actor, "Alex Morgan", "alex.morgan@example.com", false, true, 1)); tx.Fleet.Associations.Add(new(Guid.NewGuid(), vehicle, driver, DateTimeOffset.UtcNow.AddHours(-1), null)); return 0; });
        using var keys = new Discovery(); await using var factory = CreateFactory(keys).WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.Replace(ServiceDescriptor.Singleton<IFleetStore>(store)); s.Replace(ServiceDescriptor.Singleton<IIncidentStore>(store));
            s.Replace(ServiceDescriptor.Singleton<INotificationStore>(store)); s.Replace(ServiceDescriptor.Singleton<IKnowledgeStore>(store));
            s.Replace(ServiceDescriptor.Singleton<IOntologyStore>(store)); s.Replace(ServiceDescriptor.Singleton<IFaultTaxonomy>(new TestTaxonomy()));
            s.Replace(ServiceDescriptor.Singleton<IFaultEmailAgent>(new DisabledAgent()));
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthorizationTests.Token(keys.Key, "faults.read faults.write knowledge.read knowledge.publish vehicles.read vehicles.write drivers.read drivers.write", ["Reader", "Operator", "Engineer"]));
        // Drivers and vehicles keep their public CRUD operations without separate association routes.
        var d = await Send(client, HttpMethod.Post, "/api/v1/drivers", new { identifier = "DRV-NEW", ownerObjectId = Client, displayName = "Jordan Lee", email = "jordan.lee@example.com", emailNotifications = false }, status: 201); var did = d.GetProperty("id").GetGuid();
        await Send(client, HttpMethod.Get, $"/api/v1/drivers/{did}");
        await Send(client, HttpMethod.Put, $"/api/v1/drivers/{did}", new { displayName = "Jordan Lee", email = "jordan.lee@example.com", emailNotifications = false }, "\"1\"");
        await Send(client, HttpMethod.Delete, $"/api/v1/drivers/{did}", version: "\"2\"");
        Assert.False((await Send(client, HttpMethod.Get, $"/api/v1/drivers/{did}")).GetProperty("active").GetBoolean());
        var v = await Send(client, HttpMethod.Post, "/api/v1/vehicles", new { identifier = "SYN-NEW", system = "EPS" }, status: 201); var vid = v.GetProperty("id").GetGuid();
        await Send(client, HttpMethod.Get, $"/api/v1/vehicles/{vid}");
        await Send(client, HttpMethod.Put, $"/api/v1/vehicles/{vid}", new { identifier = "SYN-UPDATED", system = "EPS" }, "\"1\"");
        await Send(client, HttpMethod.Delete, $"/api/v1/vehicles/{vid}", version: "\"2\"");
        Assert.False((await Send(client, HttpMethod.Get, $"/api/v1/vehicles/{vid}")).GetProperty("active").GetBoolean());
        // Minimal intake persists both records. The disabled test provider is explicitly not delivery evidence.
        var incident = await Send(client, HttpMethod.Post, "/api/v1/incidents", new { vehicleId = vehicle, code = "SYN-EPS:UNKNOWN" }, status: 201); var iid = incident.GetProperty("incidentId").GetGuid(); var nid = incident.GetProperty("notificationId").GetGuid();
        await Send(client, HttpMethod.Get, $"/api/v1/incidents/{iid}");
        await Send(client, HttpMethod.Put, $"/api/v1/incidents/{iid}", new { status = "Investigating" }, "\"1\"");
        var n = await Send(client, HttpMethod.Get, $"/api/v1/notifications/{nid}"); Assert.Equal(0, n.GetProperty("attempts").GetArrayLength());
        var nv = n.GetProperty("version").GetInt64();
        n = await Send(client, HttpMethod.Put, $"/api/v1/notifications/{nid}", new { state = "Cancelled" }, $"\"{nv}\"");
        await Send(client, HttpMethod.Delete, $"/api/v1/notifications/{nid}", version: $"\"{n.GetProperty("version").GetInt64()}\"");
        await Send(client, HttpMethod.Get, $"/api/v1/notifications/{nid}", status: 404);
        var followup = await Send(client, HttpMethod.Post, "/api/v1/notifications", new { incidentId = iid }, status: 201); Assert.NotEqual(nid, followup.GetProperty("id").GetGuid());
        await Send(client, HttpMethod.Delete, $"/api/v1/incidents/{iid}", version: "\"2\"");
        await Send(client, HttpMethod.Get, $"/api/v1/incidents/{iid}", status: 404);
        // Fault definitions are persisted and retired; taxonomy is a declared test adapter.
        var content = new { description = "Demo steering investigation", classificationId = "demo-EPS", classificationVersion = 1, symptoms = new[] { "Reduced assist" }, references = new[] { new { id = "SYN-REF", version = "1" } }, remedies = Array.Empty<object>() };
        var f = await Send(client, HttpMethod.Post, "/api/v1/fault-codes", new { code = "SYN-EPS:TEST", system = "EPS", content }, status: 201); var fid = f.GetProperty("id").GetGuid();
        await Send(client, HttpMethod.Get, $"/api/v1/fault-codes/{fid}"); await Send(client, HttpMethod.Put, $"/api/v1/fault-codes/{fid}", content, "\"1\""); await Send(client, HttpMethod.Delete, $"/api/v1/fault-codes/{fid}", version: "\"2\"");
        Assert.True((await Send(client, HttpMethod.Get, $"/api/v1/fault-codes/{fid}")).GetProperty("retired").GetBoolean());
        var ontology = new { concepts = new[] { new { id = "SYN-EPS", kind = "subsystem", system = "EPS", label = "Electric power steering", allowedRemedyKinds = Array.Empty<string>() } }, relationships = Array.Empty<object>(), rules = Array.Empty<object>() };
        var o = await Send(client, HttpMethod.Post, "/api/v1/ontology", ontology, status: 201); var oid = o.GetProperty("id").GetGuid();
        await Send(client, HttpMethod.Get, $"/api/v1/ontology/{oid}"); await Send(client, HttpMethod.Put, $"/api/v1/ontology/{oid}", ontology, "\"1\""); await Send(client, HttpMethod.Delete, $"/api/v1/ontology/{oid}", version: "\"2\""); await Send(client, HttpMethod.Get, $"/api/v1/ontology/{oid}", status: 404);
        var restored = await Send(client, HttpMethod.Post, "/api/v1/ontology", ontology, status: 201); Assert.Equal(oid, restored.GetProperty("id").GetGuid()); Assert.Equal(4, restored.GetProperty("version").GetInt64());
        foreach (var resource in new[] { "incidents", "notifications", "vehicles", "drivers", "fault-codes", "ontology" }) await Send(client, HttpMethod.Get, "/api/v1/" + resource);
    }
    [Theory]
    [InlineData("incidents")]
    [InlineData("notifications")]
    [InlineData("drivers")]
    [InlineData("vehicles")]
    [InlineData("fault-codes")]
    [InlineData("ontology")]
    public async Task CRUD_requires_exact_grants_and_invalid_input_never_bypasses_authorization(string resource)
    {
        using var keys = new Discovery(); await using var factory = CreateFactory(keys); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthorizationTests.Token(keys.Key, "authorization.read", ["Reader"]));
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
        {
            var path = "/api/v1/" + resource + (method == HttpMethod.Put || method == HttpMethod.Delete ? "/" + Guid.NewGuid() : "");
            await Send(client, method, path, new { }, "\"1\"", 403);
        }
    }
    private sealed class DisabledAgent : IFaultEmailAgent { public Task Run(FaultEmailTool tool, CancellationToken ct) => throw new InvalidOperationException("Deliberately disabled test provider"); }
}
