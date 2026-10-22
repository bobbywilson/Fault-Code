using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace FaultCode.Api.Contracts;

internal static class FleetDocumentExamples
{
    internal static void Decorate(OpenApiOperation operation, string path)
    {
        operation.Description += " Authorized tenant-filtered fleet records. Lists are paginated after ownership filtering. Vehicles include associated drivers visible to the caller and fault codes when knowledge.read is granted. Creates use Idempotency-Key; updates and deletes require a strong If-Match version. DELETE deactivates the record and retains historical relationships.";
        foreach (var parameter in operation.Parameters ?? [])
            if (parameter is OpenApiParameter concrete && parameter.In == ParameterLocation.Query)
                concrete.Name = char.ToLowerInvariant(parameter.Name![0]) + parameter.Name[1..];
        if (operation.RequestBody?.Content is { } content)
        {
            var requestExample = operation.OperationId switch
            {
                "CreateDriver" => """{"identifier":"DRV-001","ownerObjectId":"33333333-3333-4333-8333-333333333333","displayName":"Alex Morgan","email":"driver@example.com","emailNotifications":true}""",
                "UpdateDriver" => """{"displayName":"Alex Morgan","email":"driver@example.com","emailNotifications":true}""",
                "AssociateVehicle" => """{"driverId":"55555555-5555-4555-8555-555555555555"}""",
                _ => """{"identifier":"SYN-001","system":"EPS"}"""
            };
            foreach (var media in content.Values) media.Example = JsonNode.Parse(requestExample);
        }
        foreach (var parameter in operation.Parameters ?? [])
            if (parameter is OpenApiParameter concrete && parameter.In == ParameterLocation.Header)
            {
                concrete.Example = JsonValue.Create(parameter.Name == "If-Match" ? "\"1\"" : "demo-create-001");
                concrete.Description = parameter.Name == "If-Match" ? "Single strong quoted positive resource version; stale returns 409, malformed/missing returns 400." : "1-100 ASCII letters, digits, hyphens or underscores; same actor/client/operation key and normalized payload replays atomically; changed payload returns 409.";
            }
        foreach (var (status, response) in operation.Responses!)
        {
            if (response is not OpenApiResponse concrete || concrete.Content is null) continue;
            var success = status is "200" or "201";
            concrete.Headers ??= new Dictionary<string, IOpenApiHeader>();
            concrete.Headers["X-Correlation-ID"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            if (status == "201") concrete.Headers["Location"] = new OpenApiHeader { Description = "Canonical API resource URL; association creation identifies the vehicle.", Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            if (success && operation.OperationId is not ("ListDrivers" or "ListVehicles" or "ListVehicleAssociations"))
                concrete.Headers["ETag"] = new OpenApiHeader { Description = "Strong resource version, e.g. \"1\". Association mutations return the vehicle ETag.", Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            foreach (var media in concrete.Content.Keys.ToArray())
                if (media != (success ? "application/json" : "application/problem+json")) concrete.Content.Remove(media);
            var driver = """{"id":"55555555-5555-4555-8555-555555555555","identifier":"DRV-001","displayName":"Alex Morgan","email":"driver@example.com","emailNotifications":true,"active":true,"version":1}""";
            var vehicle = """{"id":"66666666-6666-4666-8666-666666666666","identifier":"SYN-001","system":"EPS","active":true,"version":1}""";
            var association = """{"id":"77777777-7777-4777-8777-777777777777","vehicleId":"66666666-6666-4666-8666-666666666666","driverId":"55555555-5555-4555-8555-555555555555","from":"2026-10-20T12:00:00Z","until":null}""";
            var item = path.Contains("drivers", StringComparison.Ordinal) ? driver : vehicle;
            var example = operation.OperationId switch
            {
                "GetDriver" or "GetVehicle" => item,
                "ListDrivers" or "ListVehicles" => "{\"items\":[" + item + "],\"page\":1,\"pageSize\":20,\"totalCount\":1}",
                "ListVehicleAssociations" => "{\"items\":[" + association + "],\"page\":1,\"pageSize\":20,\"totalCount\":1}",
                _ => """{"id":"66666666-6666-4666-8666-666666666666","version":1}"""
            };
            foreach (var media in concrete.Content.Values)
            {
                media.Example = success ? JsonNode.Parse(example) : new JsonObject
                {
                    ["type"] = "about:blank",
                    ["title"] = "Request failed",
                    ["status"] = int.Parse(status, System.Globalization.CultureInfo.InvariantCulture),
                    ["instance"] = path,
                    ["correlationId"] = "5ce0d2ac05434c56980a058cfd2e8fa7"
                };
            }
        }
    }
}
