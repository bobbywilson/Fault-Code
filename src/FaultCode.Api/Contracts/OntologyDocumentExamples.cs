using System.Text.Json.Nodes;
using Microsoft.OpenApi;
namespace FaultCode.Api.Contracts;

internal static class OntologyDocumentExamples
{
    internal static void Decorate(OpenApiOperation operation, string path)
    {
        operation.Description += " Tenant-local ontology catalog of concepts, relationships and rules. Relationships must reference valid concepts. There is one current catalog per tenant. DELETE retires an unpublished catalog and retains history; published catalogs return 409 to protect pinned fault knowledge. The dashboard table remains read-only.";
        const string content = """{"concepts":[{"id":"SYN-EPS","kind":"subsystem","system":"EPS","label":"Electric power steering","allowedRemedyKinds":[]}],"relationships":[],"rules":[]}""";
        const string id = "55555555-5555-4555-8555-555555555555";
        if (operation.RequestBody?.Content is { } body) foreach (var media in body.Values) media.Example = JsonNode.Parse(content);
        foreach (var parameter in operation.Parameters ?? []) if (parameter is OpenApiParameter p)
        {
            if (p.In == ParameterLocation.Query) p.Name = char.ToLowerInvariant(p.Name![0]) + p.Name[1..];
            if (p.In == ParameterLocation.Header) p.Example = JsonValue.Create(p.Name == "If-Match" ? "\"1\"" : "demo-ontology-1");
        }
        foreach (var (code, response) in operation.Responses!) if (response is OpenApiResponse r && r.Content is { } mediaTypes)
        {
            var success = code.StartsWith('2');
            foreach (var key in mediaTypes.Keys.ToArray()) if (key != (success ? "application/json" : "application/problem+json")) mediaTypes.Remove(key);
            var record = "{\"id\":\"" + id + "\",\"version\":1,\"draft\":" + content + "}";
            foreach (var media in mediaTypes.Values) media.Example = !success
                ? new JsonObject { ["type"] = "about:blank", ["title"] = "Request failed", ["status"] = int.Parse(code, System.Globalization.CultureInfo.InvariantCulture), ["instance"] = path, ["correlationId"] = "demo-correlation" }
                : JsonNode.Parse(operation.OperationId == "ListOntology" ? "{\"items\":[" + record + "],\"page\":1,\"pageSize\":20,\"totalCount\":1}"
                    : operation.OperationId == "GetOntology" ? record : "{\"id\":\"" + id + "\",\"version\":1}");
        }
    }
}
