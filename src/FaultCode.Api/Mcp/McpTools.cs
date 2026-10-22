using FaultCode.Application.Telemetry;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using FaultCode.Api.Authorization;
using FaultCode.Api.Contracts;
using FaultCode.Api.Controllers;
using FaultCode.Application.Fleet;
using FaultCode.Application.Incidents;
using FaultCode.Application.Knowledge;
using FaultCode.Application.Retrieval;
using FaultCode.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace FaultCode.Api.Mcp;

public sealed class McpOptions
{
    public int TimeoutSeconds { get; set; } = 30;
}
public sealed record ToolOutcome<T>(int Status, string CorrelationId, string? FailureCode, T? Data);

// A fixed registry is the only source of tools. Nothing is loaded from caller or model content.
public sealed class McpTools(IHttpContextAccessor accessor, IAuthorizationService authorization,
    IOptions<McpOptions> options, ILogger<McpTools> logger, ITelemetry telemetry, FaultEmailService emails)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { TypeInfoResolver = new DefaultJsonTypeInfoResolver(), PropertyNameCaseInsensitive = false, RespectRequiredConstructorParameters = true };
    private sealed record Definition(Tool Tool, string[] Policies);
    private static readonly Definition[] Definitions = BuildDefinitions();
    private HttpContext Http => accessor.HttpContext ?? throw new InvalidOperationException();
    private FleetCaller Caller => new(PermissionHandler.Identifier(Http.User, "tid") ?? "", PermissionHandler.Identifier(Http.User, "oid") ?? "",
        PermissionHandler.Identifier(Http.User, "azp") ?? "", Http.TraceIdentifier,
        Allows("drivers.read"), Allows("drivers.read.own"), Allows("vehicles.read"), Allows("vehicles.read.own"));
    private bool Allows(string permission) => Permissions.Allows(Http.User, Permissions.Find(permission)!);
    private async Task<bool> Permitted(Definition d)
    {
        foreach (var policy in d.Policies)
            if (!(await authorization.AuthorizeAsync(Http.User, null, policy)).Succeeded) return false;
        return true;
    }
    public async ValueTask<ListToolsResult> List(CancellationToken ct)
    {
        var tools = new List<Tool>();
        foreach (var d in Definitions) { ct.ThrowIfCancellationRequested(); if (await Permitted(d)) tools.Add(d.Tool); }
        return new() { Tools = tools };
    }
    public async ValueTask<CallToolResult> Call(CallToolRequestParams? request, CancellationToken ct)
    {
        using var operation = telemetry.Begin(TelemetryOperation.Mcp, Http.TraceIdentifier);
        var name = request?.Name;
        var d = Definitions.SingleOrDefault(x => x.Tool.Name == name);
        if (d is null) { operation.Complete(TelemetryOutcome.Rejected); return Outcome(404, "tool-not-found", null); }
        var status = 500;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, Http.RequestAborted);
        linked.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try
        {
            if (!await Permitted(d)) return Finish(403, "tool-forbidden");
            var a = Validate(request!.Arguments, d.Tool.InputSchema);
            linked.Token.ThrowIfCancellationRequested();
            object data;
            switch (name)
            {
                case "notification_fault_lookup":
                case "notification_email_send":
                    if (Http.Items[typeof(NotificationToolContext)] is not NotificationToolContext bound) return Finish(403, "tool-workflow-binding-required");
                    data = name == "notification_fault_lookup"
                        ? await emails.Lookup(Caller, bound.NotificationId, bound.Lease, linked.Token)
                        : await emails.Send(Caller, bound.NotificationId, bound.Lease, Text(a, "summary"), Text(a, "action"), Text(a, "tone"), linked.Token);
                    break;
                default: return Finish(404, "tool-not-found");
            }
            return Finish(200, null, data);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested || Http.RequestAborted.IsCancellationRequested) { status = 499; throw; }
        catch (OperationCanceledException) { return Finish(504, "tool-timeout"); }
        catch (IncidentException e) { return Finish(e.Status, Code(e.Status)); }
        catch (KnowledgeException e) { return Finish(e.Status, Code(e.Status)); }
        catch (FleetException e) { return Finish(e.Status, Code(e.Status)); }
        catch (Exception e) when (e is JsonException or ArgumentException or FormatException or OverflowException) { return Finish(400, "tool-invalid-input"); }
        catch (Exception) { return Finish(500, "tool-failed"); }
        finally
        {
            operation.Complete(status == 499 ? TelemetryOutcome.Cancelled : status >= 500 ? TelemetryOutcome.Failed : status >= 400 ? TelemetryOutcome.Rejected : TelemetryOutcome.Success);
            // Never log arguments, keys, intent headers, source text, provider errors, or result payloads.
            logger.LogInformation("MCP tool={Tool} status={Status} actor={Actor} client={Client} correlationId={CorrelationId}",
                d.Tool.Name, status, PermissionHandler.Identifier(Http.User, "oid"), PermissionHandler.Identifier(Http.User, "azp"), Http.TraceIdentifier);
        }
        CallToolResult Finish(int code, string? failure, object? data = null) { status = code; return Outcome(code, failure, data); }
    }
    private CallToolResult Outcome(int status, string? code, object? data)
    {
        var value = JsonSerializer.SerializeToNode(new ToolOutcome<object>(status, Http.TraceIdentifier, code, data), Json)!;
        return new() { IsError = status != 200, StructuredContent = JsonSerializer.SerializeToElement(value), Content = [new TextContentBlock { Text = value.ToJsonString(Json) }] };
    }
    private static string Code(int status) => status switch
    { 400 => "tool-invalid-input", 403 => "tool-forbidden", 404 => "tool-not-found", 409 => "tool-conflict", 503 => "tool-dependency-unavailable", _ => "tool-failed" };
    private static string Text(IReadOnlyDictionary<string, JsonElement> args, string name) => args[name].GetString()!;
    private static Guid Id(IReadOnlyDictionary<string, JsonElement> args) => Guid.ParseExact(Text(args, "id"), "D");
    private static IReadOnlyDictionary<string, JsonElement> Validate(IDictionary<string, JsonElement>? args, JsonElement schema)
    {
        if (args is null || JsonSerializer.SerializeToUtf8Bytes(args).Length > 16384) throw new JsonException();
        var properties = schema.GetProperty("properties");
        foreach (var required in schema.GetProperty("required").EnumerateArray())
            if (!args.ContainsKey(required.GetString()!)) throw new JsonException();
        foreach (var (name, value) in args)
        {
            if (!properties.TryGetProperty(name, out var rule)) throw new JsonException();
            if (rule.GetProperty("type").GetString() == "string")
            {
                if (value.ValueKind != JsonValueKind.String) throw new JsonException();
                var text = value.GetString()!;
                if (text.Length < 1 || text.Length > rule.GetProperty("maxLength").GetInt32() || text.Any(ch => char.IsControl(ch) && ch is not ('\r' or '\n' or '\t'))) throw new JsonException();
                if (rule.TryGetProperty("enum", out var choices) && !choices.EnumerateArray().Any(x => x.GetString() == text)) throw new JsonException();
                if (name == "id" && (!Guid.TryParseExact(text, "D", out var id) || id == Guid.Empty || id.ToString("D") != text)) throw new JsonException();
                if (name == "idempotencyKey" && !FaultCode.Domain.IncidentRules.Token(text)) throw new JsonException();
            }
            else if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var number) || number < rule.GetProperty("minimum").GetInt64() || number > rule.GetProperty("maximum").GetInt64()) throw new JsonException();
        }
        return new Dictionary<string, JsonElement>(args, StringComparer.Ordinal);
    }
    private static Definition[] BuildDefinitions()
    {
        JsonObject Str(int max) => new() { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = max };
        JsonObject Enum(params string[] items) { var s = Str(32); s["enum"] = new JsonArray(items.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()); return s; }
        Definition Make<T>(string name, string description, JsonObject properties, string[] policies, bool readOnly)
        {
            var required = new JsonArray(properties.Select(p => p.Key).Where(k => k != "limit").Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
            var schema = new JsonObject { ["type"] = "object", ["properties"] = properties, ["required"] = required, ["additionalProperties"] = false };
            return new(new Tool
            {
                Name = name,
                Description = description,
                InputSchema = JsonSerializer.SerializeToElement(schema),
                OutputSchema = JsonSerializer.SerializeToElement(Json.GetJsonSchemaAsNode(typeof(ToolOutcome<T>))),
                Annotations = new ToolAnnotations { ReadOnlyHint = readOnly, DestructiveHint = !readOnly, IdempotentHint = true, OpenWorldHint = false }
            }, policies);
        }
        return [
            Make<FaultEmailFacts>("notification_fault_lookup", "For the server-bound notification, look up the exact code in Qdrant and revalidate the approved SQL version. Unknown is explicit; an unavailable index is an error, never a diagnosis.", new(), ["faults.write", "faults.read", "knowledge.read", "vehicles.access.read", "drivers.access.read"], true),
            Make<string>("notification_email_send", "Compose a driver email using EXACT summary/action from lookup and friendly/formal tone. Only the server-bound driver and private Mailpit transport are permitted. Records actual attempts, receipt and telemetry; duplicate/unauthorized sends are rejected.", new() { ["summary"] = Str(3000), ["action"] = Str(4000), ["tone"] = Enum("friendly", "formal") }, ["faults.write", "faults.read", "knowledge.read", "vehicles.access.read", "drivers.access.read"], false)
        ];
    }
}
