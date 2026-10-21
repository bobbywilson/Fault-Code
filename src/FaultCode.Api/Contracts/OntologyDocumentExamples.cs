using System.Text.Json;
using System.Text.Json.Nodes;
using FaultCode.Domain;
using Microsoft.OpenApi;

namespace FaultCode.Api.Contracts;

internal static class OntologyDocumentExamples
{
    internal static void Decorate(OpenApiOperation operation, string path)
    {
        operation.Description += " Tenant-local synthetic ontology. Draft replacement manages concepts, relationships and rules as one validated aggregate; omission removes draft entries only. knowledge.publish manages drafts/history/approval; knowledge.read reads approved versions and evaluates. Evaluation requires an explicit approved publicationVersion and stores the immutable result with a server ID. Priority then specificity then ordinal rule ID determines precedence. No-match is explicit; results are rule evidence, not driver guidance. Production store503 pending DEC-001. Strong versions and actor/client/operation/canonical-payload replay keys apply where declared.";
        var content = new OntologyContent([
            new("SYN-EPS-SYMPTOM", "symptom", "EPS", "Synthetic reduced assist", []),
            new("SYN-EPS-CLASS", "classification", "EPS", "Synthetic assist classification", ["dealership"]),
            new("SYN-EPS-SEVERITY", "severity", "EPS", "Synthetic inspection severity", [])], [],
            [new("SYN-EPS-RULE", "EPS", ["SYN-EPS-SYMPTOM"], "SYN-EPS-CLASS", "SYN-EPS-SEVERITY", ["dealership"], 10, null, null)]);
        var id = Guid.Parse("55555555-5555-4555-8555-555555555555");
        const string actor = "33333333-3333-4333-8333-333333333333";
        var at = DateTimeOffset.Parse("2026-10-20T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var input = new OntologyInput(2, "EPS", ["SYN-EPS-SYMPTOM"], "SYN-MODEL-1", "1.0");
        var evaluation = new OntologyEvaluation(id, 2, input, OntologyRules.Evaluate(content, input), actor, at, actor, at);
        JsonNode Node(object value) => JsonSerializer.SerializeToNode(value, value.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        if (operation.RequestBody?.Content is { } request)
            foreach (var media in request.Values) media.Example = Node(operation.OperationId == "EvaluateOntology" ? input : content);
        foreach (var parameter in operation.Parameters ?? [])
            if (parameter is OpenApiParameter concrete)
            {
                if (parameter.In == ParameterLocation.Query) concrete.Name = char.ToLowerInvariant(parameter.Name![0]) + parameter.Name[1..];
                if (parameter.In == ParameterLocation.Header)
                {
                    concrete.Example = JsonValue.Create(parameter.Name == "If-Match" ? "\"1\"" : "synthetic-ontology-001");
                    concrete.Description = parameter.Name == "If-Match" ? "One strong quoted positive aggregate version; malformed400, stale409." : "1-100 ASCII letters/digits/hyphens/underscores; exact retry replays original result, changed payload409.";
                }
            }
        foreach (var (status, response) in operation.Responses!)
        {
            if (response is not OpenApiResponse concrete || concrete.Content is null) continue;
            var success = status is "200" or "201";
            concrete.Headers ??= new Dictionary<string, IOpenApiHeader>();
            concrete.Headers["X-Correlation-ID"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            if (success && operation.OperationId != "OntologyHistory") concrete.Headers["ETag"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            if (status == "201") concrete.Headers["Location"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            foreach (var media in concrete.Content.Keys.ToArray())
                if (media != (success ? "application/json" : "application/problem+json")) concrete.Content.Remove(media);
            object example = operation.OperationId switch
            {
                "GetOntologyDraft" => new OntologyDraftResponse(id, 1, OntologyResponses.Content(content)),
                "GetApprovedOntology" => OntologyResponses.Publication(new OntologyPublication(2, content, actor, at)),
                "OntologyHistory" => new PageResponse<OntologyRevisionResponse>([new(1, OntologyResponses.Content(content), "draft", actor, at)], 1, 20, 1),
                "EvaluateOntology" or "GetOntologyEvaluation" => OntologyResponses.Evaluation(evaluation),
                _ => new MutationResponse(id, operation.OperationId == "PublishOntology" ? 2 : 1)
            };
            foreach (var media in concrete.Content.Values) media.Example = success ? Node(example)
                : new JsonObject { ["type"] = "about:blank", ["title"] = "Request failed", ["status"] = int.Parse(status, System.Globalization.CultureInfo.InvariantCulture), ["instance"] = path, ["correlationId"] = "5ce0d2ac05434c56980a058cfd2e8fa7" };
        }
    }
}
