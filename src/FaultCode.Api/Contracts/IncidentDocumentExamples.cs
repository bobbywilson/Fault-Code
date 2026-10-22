using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
namespace FaultCode.Api.Contracts;

internal static class IncidentDocumentExamples
{
    internal static void Decorate(OpenApiOperation operation, string path)
    {
        operation.Description += " Vehicle-originated incidents. POST accepts vehicleId and code, creates an incident and linked notification, then attempts agent/MCP processing. Creation does not prove email delivery. PUT updates investigation status; resolving requires resolution notes (maximum 2000 characters), records them with author/time/version, indexes case evidence in Qdrant and queues an agent/MCP follow-up notification. An unknown fault can be resolved with recorded findings; this does not publish an approved fault definition or confirm vehicle repair. DELETE removes the record from normal reads while retaining audit and delivery history. Conditional mutations require If-Match and Idempotency-Key.";
        const string id = "55555555-5555-4555-8555-555555555555";
        const string at = "2026-10-20T12:00:00Z";
        var incident = new
        {
            id,
            sourceClientId = "22222222-2222-4222-8222-222222222222",
            sourceEventId = "SYN-EVENT-1",
            code = "SYN-EPS:001",
            system = "EPS",
            model = "SYN-MODEL-1",
            softwareVersion = "1.0",
            occurredAt = at,
            receivedAt = at,
            intakeCorrelationId = "5ce0d2ac05434c56980a058cfd2e8fa7",
            fleet = new { vehicleId = id, vehicleIdentifier = "SYN-VEHICLE-1", vehicleVersion = 1, driverId = id, driverIdentifier = "DRV-1", driverVersion = 1, associationId = id, associationFrom = at, associationUntil = (string?)null },
            knowledge = (object?)null,
            status = "Received",
            version = 1,
            work = new[] { new { id, kind = "investigate", state = "Pending", attempts = 0, failureCode = (string?)null } }
        };
        if (operation.RequestBody?.Content is { } request)
            foreach (var media in request.Values) media.Example = operation.OperationId switch
            {
                "CreateIncident" => JsonSerializer.SerializeToNode(new { vehicleId = id, code = "SYN-EPS:ASSIST-SIGNAL" }),
                "UpdateIncident" => JsonSerializer.SerializeToNode(new { status = "Resolved", resolution = "Investigation confirmed the connector issue. Arrange a qualified service assessment." }),
                _ => JsonSerializer.SerializeToNode(new { code = "dispatch-unavailable" })
            };
        foreach (var parameter in operation.Parameters ?? [])
            if (parameter is OpenApiParameter concrete)
            {
                if (parameter.In == ParameterLocation.Query) concrete.Name = char.ToLowerInvariant(parameter.Name![0]) + parameter.Name[1..];
                if (parameter.In == ParameterLocation.Header)
                {
                    concrete.Example = JsonValue.Create(parameter.Name == "If-Match" ? "\"1\"" : "demo-incident-1");
                    concrete.Description = parameter.Name == "If-Match" ? "One strong quoted positive version, malformed400/stale409." : "1-100 ASCII alnum/hyphen/underscore. Exact canonical retry replays original result; changed payload409.";
                }
            }
        foreach (var (status, response) in operation.Responses!)
        {
            if (response is not OpenApiResponse concrete || concrete.Content is null) continue;
            var success = status is "200" or "201";
            concrete.Headers ??= new Dictionary<string, IOpenApiHeader>();
            concrete.Headers["X-Correlation-ID"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            if (success && operation.OperationId is not ("ListIncidents" or "IncidentHistory"))
                concrete.Headers["ETag"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            if (status == "201") concrete.Headers["Location"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            foreach (var media in concrete.Content.Keys.ToArray())
                if (media != (success ? "application/json" : "application/problem+json")) concrete.Content.Remove(media);
            object example = operation.OperationId switch
            {
                "GetIncident" => incident,
                "CreateIncident" => new { incidentId = id, notificationId = id, state = "Accepted" },
                "ListIncidents" => new { items = new[] { incident }, page = 1, pageSize = 20, totalCount = 1 },
                "IncidentHistory" => new { items = new[] { new { version = 1, status = "Received", action = "received", actorId = id, clientId = id, correlationId = "5ce0d2ac05434c56980a058cfd2e8fa7", at, failureCode = (string?)null } }, page = 1, pageSize = 20, totalCount = 1 },
                _ => new { id, version = 1 }
            };
            foreach (var media in concrete.Content.Values)
                media.Example = success ? JsonSerializer.SerializeToNode(example) : new JsonObject { ["type"] = "about:blank", ["title"] = "Request failed", ["status"] = int.Parse(status, System.Globalization.CultureInfo.InvariantCulture), ["instance"] = path, ["correlationId"] = "5ce0d2ac05434c56980a058cfd2e8fa7" };
        }
    }
}
