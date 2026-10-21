using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
namespace FaultCode.Api.Contracts;

internal static class AgentDocumentExamples
{
    internal static void Decorate(OpenApiOperation operation, string path)
    {
        operation.Description += " Ephemeral bounded advisory runtime; no incident mutation or durable run history. Exact faults.read, vehicles.access.read and knowledge.read plus trusted intake ownership. Empty request only; no supplied identity/prompt/tools. Disabled503 until explicit snapshot/secret; production incident storage503 pending DEC-001. Engineering review required; output is not approved guidance or completed investigation. Each call incurs a new run; no idempotent/billing replay is claimed.";
        if (operation.RequestBody?.Content is { } request)
            foreach (var media in request.Values) media.Example = new JsonObject();
        const string id = "55555555-5555-4555-8555-555555555555";
        foreach (var (status, response) in operation.Responses!)
        {
            if (response is not OpenApiResponse concrete || concrete.Content is null) continue;
            var success = status == "200";
            concrete.Headers ??= new Dictionary<string, IOpenApiHeader>();
            concrete.Headers["X-Correlation-ID"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            foreach (var key in concrete.Content.Keys.ToArray())
                if (key != (success ? "application/json" : "application/problem+json")) concrete.Content.Remove(key);
            foreach (var media in concrete.Content.Values)
                media.Example = success ? JsonSerializer.SerializeToNode(new
                {
                    runId = id,
                    incidentId = id,
                    correlationId = "5ce0d2ac05434c56980a058cfd2e8fa7",
                    startedAt = "2026-10-21T00:00:00Z",
                    outcome = "review-required",
                    advisory = true,
                    advice = new { incidentId = id, classification = "undetermined", evidence = Array.Empty<object>(), uncertainties = new[] { "missing-evidence" }, recommendedAction = "engineering-review", escalationRequired = true },
                    attempts = 1,
                    inputTokens = 100,
                    outputTokens = 100,
                    reservedTokens = 6000,
                    model = "synthetic-example-2026-10-20",
                    instructionsVersion = "advisory-v1"
                }) : new JsonObject { ["type"] = "about:blank", ["title"] = "Request failed", ["status"] = int.Parse(status, System.Globalization.CultureInfo.InvariantCulture), ["instance"] = path, ["correlationId"] = "5ce0d2ac05434c56980a058cfd2e8fa7" };
        }
    }
}
