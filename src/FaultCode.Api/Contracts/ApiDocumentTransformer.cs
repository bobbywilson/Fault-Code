using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FaultCode.Api.Contracts;

public sealed class ApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "Fault Code API";
        document.Info.Version = "v1";
        document.Info.Description = "Public synthetic foundation operations. Future protected operations use dedicated Entra OAuth2 authorization code + PKCE with delegated scopes, or client credentials with app roles. Tenant, audience, scope and role values will be configured in FAU-8/9; no placeholder OAuth URLs are executable.";
        document.Components!.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["EntraBearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Reserved for FAU-8/9. Delegated tokens require operation scopes; machine tokens require application roles. These public fixtures require neither."
            }
        };
        foreach (var (path, item) in document.Paths)
        {
            // Only these intentionally public fixtures belong to FAU-7. Future protected
            // operations must retain their own security metadata and examples.
            if (path is not ("/api/v1/health" or "/api/v1/examples" or "/api/v1/examples/{id}")) continue;
            foreach (var operation in item.Operations!.Values)
            {
                if (operation.OperationId is not ("GetApiHealth" or "ListExamples" or "GetExample")) continue;
                operation.Security = [];
                foreach (var parameter in operation.Parameters ?? [])
                {
                    if (parameter is OpenApiParameter concreteParameter && parameter.In == ParameterLocation.Query)
                    {
                        concreteParameter.Name = char.ToLowerInvariant(parameter.Name![0]) + parameter.Name[1..];
                        if (parameter.Schema is OpenApiSchema schema)
                        {
                            if (parameter.Name == "page") schema.Default = JsonValue.Create(1);
                            if (parameter.Name == "pageSize") schema.Default = JsonValue.Create(20);
                        }
                    }
                }
                foreach (var (status, response) in operation.Responses!)
                {
                    if (response is not OpenApiResponse concrete || concrete.Content is null) continue;
                    concrete.Headers ??= new Dictionary<string, IOpenApiHeader>();
                    concrete.Headers["X-Correlation-ID"] = new OpenApiHeader
                    {
                        Description = "Server-generated request correlation ID, also returned in Problem Details.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                    };
                    foreach (var media in concrete.Content.Keys.ToArray())
                        if (media != (status == "200" ? "application/json" : "application/problem+json")) concrete.Content.Remove(media);
                    foreach (var media in concrete.Content.Values)
                    {
                        media.Example = status != "200" ? JsonNode.Parse("""
                            {"type":"about:blank","title":"Request failed","status":400,"instance":"/api/v1/examples","correlationId":"5ce0d2ac05434c56980a058cfd2e8fa7"}
                            """) : path == "/api/v1/health" ? JsonNode.Parse("""{"status":"Healthy"}""")
                            : path.EndsWith("{id}", StringComparison.Ordinal) ? JsonNode.Parse("""{"id":"synthetic-001","system":"EPS","description":"Synthetic electric power steering example"}""")
                            : JsonNode.Parse("""{"items":[{"id":"synthetic-001","system":"EPS","description":"Synthetic electric power steering example"}],"page":1,"pageSize":1,"totalCount":3}""");
                        if (status != "200") media.Example!["status"] = int.Parse(status, System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }
        }
        if (document.Components.Schemas!.TryGetValue("ProblemDetails", out var problem) && problem is OpenApiSchema problemSchema)
        {
            problemSchema.Properties!["correlationId"] = new OpenApiSchema { Type = JsonSchemaType.String };
            problemSchema.Required = new HashSet<string> { "type", "title", "status", "instance", "correlationId" };
        }
        return Task.CompletedTask;
    }
}
