using System.Text.Json;
using System.Text.Json.Nodes;
using FaultCode.Domain;
using Microsoft.OpenApi;

namespace FaultCode.Api.Contracts;

internal static class EngineeringDocumentExamples
{
    internal static void Decorate(OpenApiOperation operation, string path)
    {
        operation.Description += " Tenant/incident-owner filtered engineering case. Strong If-Match and scoped Idempotency-Key on every mutation; creation matches incident version, subsequent mutations case version. Assignment is a work label, not an Entra grant. Separate proposal and approval; approval requires escalations.write AND knowledge.publish and commits knowledge/incident/event atomically. Events are retained historical Pending intents requiring FAU-19 revalidation and deduplication, never delivery or current dispatch authority. Production503 pending DEC-001/003; test memory is not durable. Existing MCP lifecycle escalation remains unchanged.";
        var id = Guid.Parse("55555555-5555-4555-8555-555555555555"); var at = DateTimeOffset.Parse("2026-10-21T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var e = new EngineeringCase(id, "11111111-1111-4111-8111-111111111111", id, id, "UnknownFault", 1, "Open", 1, null, null, [], [], []);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonNode Node<T>(T value) => JsonSerializer.SerializeToNode(value, options)!;
        JsonNode Page(JsonNode item) => new JsonObject { ["items"] = new JsonArray(item), ["page"] = 1, ["pageSize"] = 20, ["totalCount"] = 1 };
        var content = new FaultContent("Synthetic approved engineering finding", "SYN-CLASS", 2, ["SYN-SYM"], [new("SYN-DOC", "1")],
            [new("SYN-REMEDY", "guidance", "Synthetic engineering inspection guidance", true, "SYN-MODEL-1", "1.0", null)]);
        if (operation.RequestBody?.Content is { } request)
            foreach (var media in request.Values) media.Example = operation.OperationId switch
            {
                "CreateEngineeringCase" => Node(new CreateEngineeringCaseRequest(id, id)),
                "AssignEngineeringCase" => Node(new EngineeringAssignmentRequest("33333333-3333-4333-8333-333333333333")),
                "AddEngineeringNote" => Node(new EngineeringNoteRequest("Synthetic engineering findings")),
                "ChangeEngineeringStatus" => Node(new EngineeringStatusRequest("Investigating")),
                "ProposeEngineeringResolution" => Node(new EngineeringResolutionRequest(EngineeringProposalResponse.ContentFrom(content), 0)),
                _ => new JsonObject()
            };
        foreach (var parameter in operation.Parameters ?? [])
            if (parameter is OpenApiParameter p)
            {
                if (p.In == ParameterLocation.Query) p.Name = char.ToLowerInvariant(p.Name![0]) + p.Name[1..];
                if (p.In == ParameterLocation.Header)
                {
                    p.Example = JsonValue.Create(p.Name == "If-Match" ? "\"1\"" : "synthetic-engineering-1");
                    p.Description = p.Name == "If-Match" ? "One strong quoted positive version; missing/malformed400, stale409. Creation uses incident version; other mutations case version."
                        : "1-100 ASCII letters/digits/hyphens/underscores. Scope tenant/actor/client/operation/payload including expected version; exact replay before dependency validation, mismatch409.";
                }
            }
        foreach (var (status, response) in operation.Responses!)
        {
            if (response is not OpenApiResponse r || r.Content is null) continue;
            var success = status is "200" or "201"; r.Headers ??= new Dictionary<string, IOpenApiHeader>();
            r.Headers["X-Correlation-ID"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            if (success && operation.OperationId is not ("ListEngineeringCases" or "EngineeringCaseHistory" or "EngineeringCaseEvents"))
                r.Headers["ETag"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            if (status == "201") r.Headers["Location"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            foreach (var media in r.Content.Keys.ToArray()) if (media != (success ? "application/json" : "application/problem+json")) r.Content.Remove(media);
            foreach (var media in r.Content.Values) media.Example = !success
                ? new JsonObject { ["type"] = "about:blank", ["title"] = "Request failed", ["status"] = int.Parse(status, System.Globalization.CultureInfo.InvariantCulture), ["instance"] = path, ["correlationId"] = "synthetic-correlation" }
                : operation.OperationId switch
                {
                    "ListEngineeringCases" => Page(Node(EngineeringCaseResponse.From(e))),
                    "GetEngineeringCase" => Node(EngineeringCaseResponse.From(e)),
                    "EngineeringCaseHistory" => Page(Node(new EngineeringHistoryResponse(1, "Open", "created", null, null, null, null, "33333333-3333-4333-8333-333333333333", "44444444-4444-4444-8444-444444444444", "synthetic-correlation", at))),
                    "EngineeringCaseEvents" => Page(Node(EngineeringEventResponse.From(new(id, id, id, id, 3, id, id, 1, at, "Pending")))),
                    _ => Node(new MutationResponse(id, 1))
                };
        }
    }
}
