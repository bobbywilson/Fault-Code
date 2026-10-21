using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
namespace FaultCode.Api.Contracts;

internal static class InvestigationDocumentExamples
{
    internal static void Decorate(OpenApiOperation operation, string path)
    {
        operation.Description += " Immutable historical investigation and pending proposal only; no dispatch or incident transition. Original actor/client history plus trusted incident ownership. POST requires empty object, strong If-Match and scoped Idempotency-Key. Production canonical storage503 pending DEC-001/003; model tools remain disabled. investigation-v1 requires exact approved text/source/version support at score>=0.85; unadjudicated evidence requires review.";
        if (operation.RequestBody?.Content is { } request) foreach (var media in request.Values) media.Example = new JsonObject();
        foreach (var (status, response) in operation.Responses!)
        {
            if (response is not OpenApiResponse concrete || concrete.Content is null) continue;
            var success = status == "200";
            concrete.Headers ??= new Dictionary<string, IOpenApiHeader>();
            concrete.Headers["X-Correlation-ID"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            foreach (var key in concrete.Content.Keys.ToArray()) if (key != (success ? "application/json" : "application/problem+json")) concrete.Content.Remove(key);
            foreach (var media in concrete.Content.Values)
                media.Example = success ? operation.OperationId == "ListIncidentInvestigations" ? new JsonObject { ["items"] = new JsonArray(Example()), ["page"] = 1, ["pageSize"] = 20, ["totalCount"] = 1 } : Example()
                    : new JsonObject { ["type"] = "about:blank", ["title"] = "Request failed", ["status"] = int.Parse(status, System.Globalization.CultureInfo.InvariantCulture), ["instance"] = path, ["correlationId"] = "synthetic-correlation" };
        }
    }
    private static JsonNode Example() => JsonSerializer.SerializeToNode(new
    {
        id = "55555555-5555-4555-8555-555555555555",
        incidentId = "55555555-5555-4555-8555-555555555555",
        incidentVersion = 1,
        createdAt = "2026-10-21T00:00:00Z",
        correlationId = "synthetic-correlation",
        classification = "UnknownFault",
        definition = (object?)null,
        rule = (object?)null,
        citations = Array.Empty<object>(),
        remedy = (object?)null,
        uncertainties = new[] { "no-approved-definition" },
        proposedAction = new { kind = "engineering-escalation", status = "Pending" },
        model = new { model = "synthetic-agent-2026-10-20", instructionsVersion = "investigation-v1", attempts = 1, inputTokens = 100, outputTokens = 100, reservedTokens = 6500, reviewRequired = false },
        policyVersion = "investigation-v1",
        evidencePolicy = "Untrusted historical source evidence; pending proposal only, no current dispatch authority."
    })!;
}
