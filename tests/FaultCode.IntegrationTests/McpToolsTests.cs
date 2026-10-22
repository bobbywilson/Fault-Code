using System.Security.Claims;
using System.Text.Json;
using FaultCode.Api.Mcp;
using FaultCode.Application.Incidents;
using FaultCode.Application.Notifications;
using FaultCode.Application.Knowledge;
using FaultCode.Application.Telemetry;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Protocol;
using static FaultCode.IntegrationTests.EntraAuthenticationTests;
using static FaultCode.IntegrationTests.EngineeringTests;
namespace FaultCode.IntegrationTests;

// Internal registry tests. No public MCP/administrative HTTP endpoint is exposed.
public sealed class McpToolsTests
{
    private static DefaultHttpContext Context(bool write = true) => new()
    {
        TraceIdentifier = "demo-mcp-correlation",
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim("tid",Tenant), new Claim("oid",Actor), new Claim("azp",Client), new Claim("roles",write ? "Operator" : "Reader"),
            new Claim("scp", "faults.read knowledge.read vehicles.read drivers.read" + (write ? " faults.write" : "")) }, "fixture"))
    };
    private static CallToolRequestParams Request(string name, object arguments) => new() { Name = name, Arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(arguments)) };
    private static JsonElement Data(CallToolResult result, int status)
    {
        var value = JsonDocument.Parse(Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text).RootElement.Clone();
        Assert.Equal(status, value.GetProperty("status").GetInt32()); Assert.Equal(status != 200, result.IsError); return value;
    }
    [Fact]
    public async Task Registry_exposes_only_bound_workflow_tools_with_exact_permissions()
    {
        using var keys = new Discovery(); await using var factory = CreateFactory(keys); using var http = factory.CreateClient();
        using var scope = factory.Services.CreateScope(); var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = Context(); var tools = scope.ServiceProvider.GetRequiredService<McpTools>();
        Assert.Equal(new[] { "notification_fault_lookup", "notification_email_send" }, (await tools.List(default)).Tools.Select(t => t.Name));
        accessor.HttpContext = Context(false); Assert.Empty((await tools.List(default)).Tools);
        Data(await tools.Call(Request("notification_fault_lookup", new { }), default), 403);
        accessor.HttpContext = Context(); Data(await tools.Call(Request("notification_fault_lookup", new { }), default), 403);
        Data(await tools.Call(Request("incident_escalate", new { }), default), 404);
    }
    [Theory]
    [InlineData("tenant")]
    [InlineData("recipient")]
    [InlineData("invalid-tone")]
    [InlineData("missing")]
    [InlineData("extra")]
    public async Task Model_arguments_cannot_select_identity_recipient_or_unbounded_actions(string kind)
    {
        using var keys = new Discovery(); await using var factory = CreateFactory(keys); using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = Context();
        object args = kind switch { "tenant" => new { tenantId = Tenant }, "recipient" => new { summary = "a", action = "b", tone = "friendly", recipient = "other@example.com" }, "invalid-tone" => new { summary = "a", action = "b", tone = "invent" }, "missing" => new { summary = "a" }, _ => new { summary = "a", action = "b", tone = "friendly", code = "other" } };
        var name = kind == "tenant" ? "notification_fault_lookup" : "notification_email_send";
        Data(await scope.ServiceProvider.GetRequiredService<McpTools>().Call(Request(name, args), default), 400);
    }
    [Fact]
    public async Task Bound_lookup_and_email_use_server_recipient_and_record_actual_delivery_attempts()
    {
        var store = new TestEngineeringStore(); var vehicle = Guid.NewGuid(); var driver = Guid.NewGuid();
        await store.NotificationExecute(Tenant, tx => { tx.Fleet.Vehicles.Add(new(vehicle, Tenant, "SYN-SILVERADO-001", "EPS", true, 1)); tx.Fleet.Drivers.Add(new(driver, Tenant, "DRV-MCP", Actor, "Alex Morgan", "alex.morgan@example.com", false, true, 1)); tx.Fleet.Associations.Add(new(Guid.NewGuid(), vehicle, driver, DateTimeOffset.UtcNow.AddHours(-1), null)); return 0; });
        var receipt = await new VehicleFaultDispatchService(store, TimeProvider.System, NullTelemetry.Instance).Dispatch(Caller, vehicle, "SYN-EPS:UNKNOWN", "intake", default);
        var capture = new Capture(); using var keys = new Discovery();
        await using var factory = CreateFactory(keys).WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.Replace(ServiceDescriptor.Singleton<INotificationStore>(store)); s.Replace(ServiceDescriptor.Singleton<IKnowledgeStore>(store));
            s.Replace(ServiceDescriptor.Singleton<IFaultCodeIndex>(new EmptyIndex())); s.Replace(ServiceDescriptor.Singleton<INotificationDelivery>(capture));
        }));
        using var client = factory.CreateClient(); using var scope = factory.Services.CreateScope(); var context = Context(); scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
        var emails = scope.ServiceProvider.GetRequiredService<FaultEmailService>(); var lease = await emails.Acquire(Caller, receipt.NotificationId, default);
        context.Items[typeof(NotificationToolContext)] = new NotificationToolContext(receipt.NotificationId, lease!.Value);
        var tools = scope.ServiceProvider.GetRequiredService<McpTools>();
        var lookup = Data(await tools.Call(Request("notification_fault_lookup", new { }), default), 200); var facts = lookup.GetProperty("data");
        var sent = Data(await tools.Call(Request("notification_email_send", new { summary = facts.GetProperty("summary").GetString(), action = facts.GetProperty("action").GetString(), tone = "friendly" }), default), 200);
        Assert.Equal("alex.morgan@example.com", Assert.Single(capture.Sends).DemoContact); Assert.Contains("investigating", capture.Sends[0].Message);
        var record = Assert.Single(store.NotificationSnapshot().Notifications.Messages); Assert.Equal("Sent", record.State); Assert.Single(record.Attempts); Assert.Equal("capture-receipt", record.Attempts[0].Acknowledgement);
        Assert.Null(await emails.Acquire(Caller, receipt.NotificationId, default)); Assert.Single(capture.Sends);
        var registry = (await tools.List(default)).Tools;
        var samples = new[] { new { name = "notification_fault_lookup", input = (object)new { }, output = (object)lookup, error = (object)Data(await tools.Call(Request("notification_fault_lookup", new { extra = true }), default), 400) }, new { name = "notification_email_send", input = (object)new { summary = facts.GetProperty("summary").GetString(), action = facts.GetProperty("action").GetString(), tone = "friendly" }, output = (object)sent, error = (object)Data(await tools.Call(Request("notification_email_send", new { extra = true }), default), 400) } };
        var root = FindRoot(); Directory.CreateDirectory(Path.Combine(root, "artifacts/mcp"));
        await File.WriteAllTextAsync(Path.Combine(root, "artifacts/mcp-contracts.json"), JsonSerializer.Serialize(new { sdk = "2.3.0", tools = registry, samples }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
    private static string FindRoot() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "FaultCode.slnx"))) d = d.Parent; return d!.FullName; }
    private sealed class EmptyIndex : IFaultCodeIndex { public Task<FaultIndexHit?> Find(string tenant, string code, string system, string model, string software, CancellationToken ct) => Task.FromResult<FaultIndexHit?>(null); public Task Upsert(string tenant, ApprovedFault fault, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Capture : INotificationDelivery { public List<LocalDelivery> Sends { get; } = []; public Task<DeliveryAcknowledgement> Send(LocalDelivery message, CancellationToken ct) { Sends.Add(message); return Task.FromResult(new DeliveryAcknowledgement("capture-receipt")); } }
}
