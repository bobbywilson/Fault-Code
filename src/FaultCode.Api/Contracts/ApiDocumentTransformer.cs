using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using FaultCode.Api.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace FaultCode.Api.Contracts;

public sealed class ApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "Fault Code API";
        document.Info.Version = "v1";
        document.Info.Description = "Versioned Fault Code API with public synthetic foundation operations and protected caller permissions. Dedicated Entra v2 bearer validation is available when configured; protected operations require delegated scopes or application roles under FAU-9. Browser clients use authorization code + PKCE; machines use client credentials. No tenant has been provisioned and no placeholder OAuth URLs are executable.";
        document.Components!.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["EntraBearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Dedicated Entra v2 access token. Protected operations require exact delegated scopes or machine application roles. These public fixtures require neither."
            }
        };
        foreach (var description in context.DocumentName == "v1" ? context.DescriptionGroups.SelectMany(g => g.Items) : [])
        {
            var policies = description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>()
                .Select(a => a.Policy).Where(p => p is not null).ToArray();
            if (policies.Length == 0) continue;
            var path = "/" + description.RelativePath;
            if (!document.Paths.TryGetValue(path, out var item)) continue;
            var method = new System.Net.Http.HttpMethod(description.HttpMethod!);
            if (!item.Operations!.TryGetValue(method, out var operation)) continue;
            {
                operation.Security = [new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("EntraBearer", document)] = []
                }];
                foreach (var (status, response) in operation.OperationId == "GetCallerPermissions" ? operation.Responses! : new OpenApiResponses())
                {
                    if (response is not OpenApiResponse concrete || concrete.Content is null) continue;
                    concrete.Headers ??= new Dictionary<string, IOpenApiHeader>();
                    concrete.Headers["X-Correlation-ID"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
                    foreach (var media in concrete.Content.Keys.ToArray())
                        if (media != (status == "200" ? "application/json" : "application/problem+json")) concrete.Content.Remove(media);
                    foreach (var media in concrete.Content.Values)
                    {
                        media.Example = status == "200" ? JsonNode.Parse("""{"callerKind":"delegated","permissions":["authorization.read"]}""")
                            : JsonNode.Parse("""{"type":"about:blank","title":"Request failed","status":401,"instance":"/api/v1/authorization/me","correlationId":"5ce0d2ac05434c56980a058cfd2e8fa7"}""");
                        if (status != "200") media.Example!["status"] = int.Parse(status, System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
                operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                operation.Extensions["x-faultcode-required-policies"] = new JsonNodeExtension(new JsonArray(policies.Select(p => (JsonNode?)JsonValue.Create(p)).ToArray()));
                var requirements = new JsonArray();
                foreach (var policy in policies)
                {
                    var readArea = policy is "drivers.access.read" ? "drivers" : policy is "vehicles.access.read" ? "vehicles" : null;
                    var known = Permissions.TryPolicy(policy!, out var permission, out var kind);
                    if (readArea is not null) permission = Permissions.Find(readArea + ".read");
                    if (!known && readArea is null) continue;
                    var alternatives = new JsonArray();
                    void Alternative(Permission grant) => alternatives.Add(new JsonObject
                    {
                        ["scope"] = grant.Scope,
                        ["humanRoles"] = new JsonArray(grant.HumanRoles.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
                        ["applicationRole"] = grant.ApplicationRole
                    });
                    Alternative(permission!);
                    if (readArea is not null) Alternative(Permissions.Find(readArea + ".read.own")!);
                    requirements.Add(new JsonObject { ["policy"] = policy, ["anyOf"] = alternatives });
                    operation.Description += $" Requires {permission!.Name}. Delegated: exact scope {permission.Scope} AND one of roles {string.Join(", ", permission.HumanRoles)}. Application: exact role {permission.ApplicationRole ?? "not permitted"}. Caller kind: {kind}.";
                    if (readArea is not null) operation.Description += $" Alternatively exact {readArea}.read.own scope AND Reader role; application own-read is unavailable. Trusted record authorization remains required.";
                    operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                    operation.Extensions["x-faultcode-permission"] = new JsonNodeExtension(JsonValue.Create(readArea is null ? permission.Name : policy)!);
                    operation.Extensions["x-delegated-scopes"] = new JsonNodeExtension(readArea is null ? new JsonArray(JsonValue.Create(permission.Scope)) : new JsonArray(JsonValue.Create(permission.Scope), JsonValue.Create(readArea + ".read.own")));
                    operation.Extensions["x-human-roles"] = new JsonNodeExtension(new JsonArray(permission.HumanRoles.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()));
                    operation.Extensions["x-application-roles"] = new JsonNodeExtension(permission.ApplicationRole is null ? new JsonArray() : new JsonArray(JsonValue.Create(permission.ApplicationRole)));
                }
                operation.Extensions["x-faultcode-policy-requirements"] = new JsonNodeExtension(requirements);
                if (policies.Length > 1)
                {
                    var grants = policies.Select(p => Permissions.Find(p == "vehicles.access.read" ? "vehicles.read" : p!)!).ToArray();
                    operation.Extensions["x-faultcode-permission"] = new JsonNodeExtension(JsonValue.Create(string.Join(" AND ", policies))!);
                    operation.Extensions["x-human-roles"] = new JsonNodeExtension(new JsonArray(grants.Select(p => p.HumanRoles.AsEnumerable()).Aggregate((a, b) => a.Intersect(b)).Select(r => (JsonNode?)JsonValue.Create(r)).ToArray()));
                    operation.Extensions["x-delegated-scopes"] = new JsonNodeExtension(new JsonArray(grants.Select(p => p.Scope).Concat(policies.Contains("vehicles.access.read") ? new[] { "vehicles.read.own" } : []).Select(p => (JsonNode?)JsonValue.Create(p)).ToArray()));
                    operation.Extensions["x-application-roles"] = new JsonNodeExtension(new JsonArray(grants.Select(p => (JsonNode?)JsonValue.Create(p.ApplicationRole)).ToArray()));
                }
                if (path.StartsWith("/api/v1/drivers", StringComparison.Ordinal) || path.StartsWith("/api/v1/vehicles", StringComparison.Ordinal))
                    FleetDocumentExamples.Decorate(operation, path);
                if (path.EndsWith("/investigations", StringComparison.Ordinal)) InvestigationDocumentExamples.Decorate(operation, path);
                else if (operation.OperationId == "RunIncidentAgent") AgentDocumentExamples.Decorate(operation, path);
                else if (path.StartsWith("/api/v1/incidents", StringComparison.Ordinal)) IncidentDocumentExamples.Decorate(operation, path);
                if (path.StartsWith("/api/v1/faults", StringComparison.Ordinal)) KnowledgeDocumentExamples.Decorate(operation, path);
                if (path.StartsWith("/api/v1/ontology", StringComparison.Ordinal)) OntologyDocumentExamples.Decorate(operation, path);
                if (path.StartsWith("/api/v1/knowledge", StringComparison.Ordinal)) RetrievalDocumentExamples.Decorate(operation, path);
                if (path.StartsWith("/api/v1/engineering-escalations", StringComparison.Ordinal)) EngineeringDocumentExamples.Decorate(operation, path);
            }
        }
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
