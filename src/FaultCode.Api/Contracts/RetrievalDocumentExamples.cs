using System.Text.Json;
using System.Text.Json.Nodes;
using FaultCode.Application.Retrieval;
using Microsoft.OpenApi;
namespace FaultCode.Api.Contracts;

internal static class RetrievalDocumentExamples
{
    internal static void Decorate(OpenApiOperation operation, string path)
    {
        operation.Description += " Bounded synthetic extracted pages, not URL/binary ingestion. knowledge.publish manages documents, immutable approvals, deletion and explicit indexing; knowledge.read searches only current approved tenant/publisher-visible exact applicability. Draft edits never replace approved evidence. Index/search503 if canonical storage, explicitly pinned embeddings or compatible Qdrant are unavailable. Production canonical store503 pending DEC-001/003. Reindex rebuilds only this tenant; retry key scopes actor/client/operation/profile. Returned source text is untrusted, conflicts unadjudicated; no generation, repair guidance, GraphRAG or tool execution.";
        var content = new DocumentContentRequest("Synthetic EPS reference", "1", "EPS", "SYN-MODEL-1", "1.0", "tenant", [new(1, "Synthetic EPS documentation")]);
        var id = Guid.Parse("55555555-5555-4555-8555-555555555555");
        JsonNode Node(object value) => JsonSerializer.SerializeToNode(value, value.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        if (operation.RequestBody?.Content is { } body)
            foreach (var media in body.Values) media.Example = Node(operation.OperationId switch
            {
                "CreateDocument" => (object)new CreateDocumentRequest("SYN-EPS-DOC", content),
                "SearchEvidence" => new SearchEvidenceRequest("Synthetic EPS assistance", "EPS", "SYN-MODEL-1", "1.0", 5),
                _ => content
            });
        foreach (var parameter in operation.Parameters ?? [])
            if (parameter is OpenApiParameter p && p.In == ParameterLocation.Header)
            {
                p.Example = JsonValue.Create(p.Name == "If-Match" ? "\"1\"" : "synthetic-document-001");
                p.Description = p.Name == "If-Match" ? "One strong positive quoted aggregate version; malformed400, stale409."
                    : "1-100 ASCII letters/digits/hyphen/underscore; scoped exact retry returns original result, changed payload409. Use a new key for a new reindex after index loss.";
            }
        foreach (var (status, response) in operation.Responses!)
        {
            if (response is not OpenApiResponse concrete || concrete.Content is null) continue;
            var success = status is "200" or "201";
            concrete.Headers ??= new Dictionary<string, IOpenApiHeader>();
            concrete.Headers["X-Correlation-ID"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            if (success && operation.OperationId is not ("SearchEvidence" or "ReindexDocuments")) concrete.Headers["ETag"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            if (status == "201") concrete.Headers["Location"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            foreach (var type in concrete.Content.Keys.ToArray()) if (type != (success ? "application/json" : "application/problem+json")) concrete.Content.Remove(type);
            object example = operation.OperationId switch
            {
                "ReindexDocuments" => new ReindexResponse(id, 1),
                "GetDocument" => new DocumentResponse(id, "SYN-EPS-DOC", 1, content, null, null),
                "SearchEvidence" => new EvidenceResponse("evidence-found", RetrievalService.EvidencePolicy,
                    [new(id, id, "SYN-EPS-DOC", "1", 2, 1, 0, new string('a', 64), new string('b', 64), "Synthetic EPS documentation", 0.95,
                        "33333333-3333-4333-8333-333333333333", DateTimeOffset.UnixEpoch)]),
                _ => new MutationResponse(id, 1)
            };
            foreach (var media in concrete.Content.Values) media.Example = success ? Node(example)
                : new JsonObject
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
