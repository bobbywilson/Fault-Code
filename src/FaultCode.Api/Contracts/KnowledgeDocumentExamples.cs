using System.Text.Json.Nodes;
using Microsoft.OpenApi;
namespace FaultCode.Api.Contracts;

internal static class KnowledgeDocumentExamples
{
    internal static void Decorate(OpenApiOperation operation, string path)
    {
        operation.Description += " Tenant-scoped fault-code definitions. Creates and updates validate fault content and ontology references. DELETE retires the definition while preserving evidence history. Publication and indexing are backend workflow operations, not additional public endpoints. Conditional writes require If-Match.";
        const string content = """{"description":"Demo EPS assist fault","classificationId":"demo-eps-assist","classificationVersion":1,"symptoms":["Demo reduced assist"],"references":[{"id":"SYN-DOC-1","version":"1"}],"remedies":[{"id":"SYN-REMEDY-1","kind":"dealership","description":"Recommend dealership inspection","approved":true,"model":"SYN-MODEL-1","softwareVersion":"1.0","patch":{"reference":"SYN-PATCH-1","softwareVersion":"1.0"}}]}""";
        const string id = "55555555-5555-4555-8555-555555555555";
        var fault = "{\"id\":\"" + id + "\",\"code\":\"SYN-EPS:001\",\"system\":\"EPS\",\"retired\":false,\"version\":1,\"draft\":" + content + "}";
        var history = "{\"version\":1,\"content\":" + content + ",\"state\":\"draft\",\"actorId\":\"33333333-3333-4333-8333-333333333333\",\"at\":\"2026-10-20T12:00:00Z\"}";
        var approved = "{\"id\":\"" + id + "\",\"code\":\"SYN-EPS:001\",\"system\":\"EPS\",\"version\":2,\"content\":" + content + ",\"approvedBy\":\"33333333-3333-4333-8333-333333333333\",\"approvedAt\":\"2026-10-20T12:00:00Z\"}";
        if (operation.RequestBody?.Content is { } request)
            foreach (var media in request.Values) media.Example = JsonNode.Parse(operation.OperationId == "CreateFaultCode"
                ? "{\"code\":\"SYN-EPS:001\",\"system\":\"EPS\",\"content\":" + content + "}" : content);
        foreach (var parameter in operation.Parameters ?? [])
            if (parameter is OpenApiParameter concrete)
            {
                if (parameter.In == ParameterLocation.Query) concrete.Name = char.ToLowerInvariant(parameter.Name![0]) + parameter.Name[1..];
                if (parameter.In == ParameterLocation.Header)
                {
                    concrete.Example = JsonValue.Create(parameter.Name == "If-Match" ? "\"1\"" : "demo-fault-001");
                    concrete.Description = parameter.Name == "If-Match" ? "One strong quoted positive version; missing/malformed400, stale409." : "1-100 ASCII letters/digits/hyphens/underscores. Identical canonical retry replays original result; changed payload409.";
                }
            }
        foreach (var (status, response) in operation.Responses!)
        {
            if (response is not OpenApiResponse concrete || concrete.Content is null) continue;
            var success = status is "200" or "201";
            concrete.Headers ??= new Dictionary<string, IOpenApiHeader>();
            concrete.Headers["X-Correlation-ID"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            if (success && operation.OperationId is not ("ListFaultCodes" or "FaultHistory"))
                concrete.Headers["ETag"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            if (status == "201") concrete.Headers["Location"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            foreach (var media in concrete.Content.Keys.ToArray())
                if (media != (success ? "application/json" : "application/problem+json")) concrete.Content.Remove(media);
            var example = operation.OperationId switch
            {
                "GetFaultCode" => fault,
                "ListFaultCodes" => "{\"items\":[" + fault + "],\"page\":1,\"pageSize\":20,\"totalCount\":1}",
                "FaultHistory" => "{\"items\":[" + history + "],\"page\":1,\"pageSize\":20,\"totalCount\":1}",
                "LookupApprovedFault" => approved,
                _ => "{\"id\":\"" + id + "\",\"version\":1}"
            };
            foreach (var media in concrete.Content.Values)
                media.Example = success ? JsonNode.Parse(example) : new JsonObject { ["type"] = "about:blank", ["title"] = "Request failed", ["status"] = int.Parse(status, System.Globalization.CultureInfo.InvariantCulture), ["instance"] = path, ["correlationId"] = "5ce0d2ac05434c56980a058cfd2e8fa7" };
        }
    }
}
