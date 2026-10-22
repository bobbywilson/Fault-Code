using System.Text.Json;
using System.Text.Json.Nodes;
using FaultCode.Domain;
using Microsoft.OpenApi;

namespace FaultCode.Api.Contracts;

internal static class NotificationDocumentExamples
{
    internal static void Decorate(OpenApiOperation operation, string path)
    {
        operation.Description += " Driver notifications linked to a vehicle incident. POST accepts incidentId and derives the recipient from that incident. PUT accepts state AwaitingAgent to retry eligible work or Cancelled to cancel unsent work; sent content and receipts cannot be overwritten. Attempts count actual sends and Sent requires a Mailpit acknowledgement. DELETE hides a notification while retaining history. If-Match and Idempotency-Key protect updates and deletes; uncertain delivery is not blindly retried.";
        var id = Guid.Parse("55555555-5555-4555-8555-555555555555");
        var at = DateTimeOffset.Parse("2026-10-21T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonNode Node<T>(T value) => JsonSerializer.SerializeToNode(value, options)!;
        JsonNode Page(JsonNode item) => new JsonObject { ["items"] = new JsonArray(item), ["page"] = 1, ["pageSize"] = 20, ["totalCount"] = 1 };
        var n = new DriverNotification(id, "", id, id, "", "investigation", id, id, 2, 2,
            new("SYN-REMEDY", "guidance", "Demo approved guidance.", true, "SYN-MODEL-1", "1.0", null), [], NotificationRules.Template,
            "Demo approved guidance.", "SYN-CONTACT", 2, 4, "Pending", 1, at, null, null, null, [], []);
        if (operation.RequestBody?.Content is { } request)
            foreach (var media in request.Values) media.Example = operation.OperationId switch
            {
                "CreateNotification" => Node(new { incidentId = id }),
                "UpdateNotification" => Node(new { state = "AwaitingAgent" }),
                "UpdateNotificationPreferences" => Node(new LocalNotificationPreferenceRequest(true, "SYN-CONTACT")),
                _ => new JsonObject()
            };
        foreach (var parameter in operation.Parameters ?? [])
            if (parameter is OpenApiParameter p)
            {
                if (p.In == ParameterLocation.Query) p.Name = char.ToLowerInvariant(p.Name![0]) + p.Name[1..];
                if (p.In == ParameterLocation.Header) p.Example = JsonValue.Create(p.Name == "If-Match" ? "\"1\"" : "demo-notification-1");
            }
        foreach (var (status, response) in operation.Responses!)
        {
            if (response is not OpenApiResponse r || r.Content is null) continue;
            var success = status is "200" or "201"; r.Headers ??= new Dictionary<string, IOpenApiHeader>();
            r.Headers["X-Correlation-ID"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            if (success && operation.OperationId is not ("ListDriverNotifications" or "ListIncidentNotifications" or "NotificationHistory"))
                r.Headers["ETag"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            if (status == "201") r.Headers["Location"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            foreach (var media in r.Content.Keys.ToArray()) if (media != (success ? "application/json" : "application/problem+json")) r.Content.Remove(media);
            foreach (var media in r.Content.Values) media.Example = !success
                ? new JsonObject { ["type"] = "about:blank", ["title"] = "Request failed", ["status"] = int.Parse(status, System.Globalization.CultureInfo.InvariantCulture), ["instance"] = path, ["correlationId"] = "demo-correlation" }
                : operation.OperationId switch
                {
                    "ListDriverNotifications" => Page(Node(new DriverNotificationSummaryResponse(id, id, id, "Alex Morgan", "alex.morgan@example.com", "Investigation update", "Pending", at, null, 0, null))),
                    "ListIncidentNotifications" => Page(Node(NotificationResponse.From(n))),
                    "GetNotification" => Node(NotificationResponse.From(n)),
                    "NotificationHistory" => Page(Node(new NotificationHistoryResponse(1, "Pending", "enqueued", at, "33333333-3333-4333-8333-333333333333", "44444444-4444-4444-8444-444444444444", "demo-correlation"))),
                    "GetNotificationPreferences" => Node(new LocalNotificationPreferenceResponse(id, false, false, 1)),
                    _ => Node(new MutationResponse(id, 1))
                };
        }
    }
}
