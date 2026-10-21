using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace FaultCode.Api.Contracts;

internal static class AdministrationDocumentExamples
{
    internal static void Decorate(OpenApiOperation operation, string path)
    {
        operation.Description += " Local administration metadata only; no Entra grant or external provider activation. Production adapter unavailable503 pending approved persistence and identity/provider integration. Mutations atomically store trusted tenant/actor/client/correlation audit and scoped replay; strong If-Match and Idempotency-Key required.";
        var user = JsonNode.Parse("""{"id":"55555555-5555-4555-8555-555555555555","displayName":"Synthetic administrator","roles":["Reader"],"version":1}""")!;
        var integration = JsonNode.Parse("""{"id":"66666666-6666-4666-8666-666666666666","name":"Recorded telemetry registration","provider":"Otlp","enabled":false,"version":1}""")!;
        foreach (var parameter in operation.Parameters ?? [])
            if (parameter is OpenApiParameter concrete)
            {
                if (parameter.In == ParameterLocation.Query) concrete.Name = char.ToLowerInvariant(parameter.Name![0]) + parameter.Name[1..];
                if (parameter.In == ParameterLocation.Header) concrete.Example = JsonValue.Create(parameter.Name == "If-Match" ? "\"1\"" : "synthetic-admin-001");
            }
        if (operation.RequestBody?.Content is { } content)
            foreach (var media in content.Values) media.Example = operation.OperationId == "ReplaceManagedUserRoles"
                ? JsonNode.Parse("""{"roles":["Reader"]}""") : JsonNode.Parse("""{"enabled":false}""");
        foreach (var (status, response) in operation.Responses!)
        {
            if (response is not OpenApiResponse concrete || concrete.Content is null) continue;
            var success = status == "200";
            concrete.Headers ??= new Dictionary<string, IOpenApiHeader>();
            concrete.Headers["X-Correlation-ID"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            if (success && !operation.OperationId!.StartsWith("List", StringComparison.Ordinal)) concrete.Headers["ETag"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            foreach (var media in concrete.Content.Keys.ToArray())
                if (media != (success ? "application/json" : "application/problem+json")) concrete.Content.Remove(media);
            var item = path.Contains("users", StringComparison.Ordinal) ? user : integration;
            foreach (var media in concrete.Content.Values)
                media.Example = !success ? new JsonObject { ["type"] = "about:blank", ["title"] = "Request failed", ["status"] = int.Parse(status, System.Globalization.CultureInfo.InvariantCulture), ["instance"] = path, ["correlationId"] = "5ce0d2ac05434c56980a058cfd2e8fa7" }
                    : operation.OperationId == "ListAdministrationRoles" ? JsonNode.Parse("""{"roles":["Reader","Operator","Engineer","Administrator"]}""")
                    : operation.OperationId!.StartsWith("List", StringComparison.Ordinal) ? new JsonObject { ["items"] = new JsonArray(item.DeepClone()), ["page"] = 1, ["pageSize"] = 20, ["totalCount"] = 1 } : item.DeepClone();
        }
    }
}
