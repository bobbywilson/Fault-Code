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
        document.Servers = [new OpenApiServer { Url = "https://faultcodeai.com" }];
        document.Info.Title = "Fault Code API";
        document.Info.Version = "v1";
        document.Info.Description = "Fault Code REST API: list, get, create, update and delete incidents, vehicles, drivers, notifications, fault codes and ontology. Send a dedicated Fault Code Entra access token in the Authorization: Bearer header. The API validates signature, issuer, audience, expiry, scopes and roles. Limit: 200 authorized API requests per identity per UTC day, shared across endpoints. Resets at midnight UTC. Refreshing a token does not reset usage; excess requests return 429 with Retry-After.";
        document.Components!.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["EntraBearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Dedicated Entra v2 access token. Every resource operation requires its declared delegated scopes and allowed user roles, or application roles."
            }
        };
        foreach (var item in document.Paths.Values)
            foreach (var operation in item.Operations!.Values)
                operation.Summary ??= System.Text.RegularExpressions.Regex.Replace(operation.OperationId ?? "API operation", "(?<=[a-z0-9])(?=[A-Z])", " ");
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
                    var grants = policies.Select(p => Permissions.Find(p == "vehicles.access.read" ? "vehicles.read" : p == "drivers.access.read" ? "drivers.read" : p!)!).ToArray();
                    operation.Extensions["x-faultcode-permission"] = new JsonNodeExtension(JsonValue.Create(string.Join(" AND ", policies))!);
                    operation.Extensions["x-human-roles"] = new JsonNodeExtension(new JsonArray(grants.Select(p => p.HumanRoles.AsEnumerable()).Aggregate((a, b) => a.Intersect(b)).Select(r => (JsonNode?)JsonValue.Create(r)).ToArray()));
                    operation.Extensions["x-delegated-scopes"] = new JsonNodeExtension(new JsonArray(grants.Select(p => p.Scope).Concat(policies.Contains("vehicles.access.read") ? new[] { "vehicles.read.own" } : []).Concat(policies.Contains("drivers.access.read") ? new[] { "drivers.read.own" } : []).Select(p => (JsonNode?)JsonValue.Create(p)).ToArray()));
                    operation.Extensions["x-application-roles"] = new JsonNodeExtension(new JsonArray(grants.Select(p => (JsonNode?)JsonValue.Create(p.ApplicationRole)).ToArray()));
                }
                operation.Description += " Daily quota: 200 authorized API requests per identity across all endpoints; resets at midnight UTC. Token refresh does not reset usage. Quota exhaustion returns 429 with Retry-After and X-RateLimit-Limit, X-RateLimit-Remaining and X-RateLimit-Reset headers.";
                operation.Responses!["429"] = new OpenApiResponse
                {
                    Description = "Daily API quota exhausted. Retry after midnight UTC.",
                    Headers = new Dictionary<string, IOpenApiHeader>
                    {
                        ["Retry-After"] = new OpenApiHeader { Description = "Seconds until midnight UTC.", Schema = new OpenApiSchema { Type = JsonSchemaType.Integer } },
                        ["X-RateLimit-Limit"] = new OpenApiHeader { Description = "Daily request allowance: 200.", Schema = new OpenApiSchema { Type = JsonSchemaType.Integer } },
                        ["X-RateLimit-Remaining"] = new OpenApiHeader { Description = "Remaining requests: 0 when rejected.", Schema = new OpenApiSchema { Type = JsonSchemaType.Integer } },
                        ["X-RateLimit-Reset"] = new OpenApiHeader { Description = "Next reset as a Unix timestamp in seconds.", Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" } }
                    },
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/problem+json"] = new OpenApiMediaType { Schema = new OpenApiSchemaReference("ProblemDetails", document) }
                    }
                };
                if (path.StartsWith("/api/v1/drivers", StringComparison.Ordinal) || path.StartsWith("/api/v1/vehicles", StringComparison.Ordinal)) FleetDocumentExamples.Decorate(operation, path);
                if (path.StartsWith("/api/v1/incidents", StringComparison.Ordinal)) IncidentDocumentExamples.Decorate(operation, path);
                if (path.StartsWith("/api/v1/fault-codes", StringComparison.Ordinal)) KnowledgeDocumentExamples.Decorate(operation, path);
                if (path.StartsWith("/api/v1/ontology", StringComparison.Ordinal)) OntologyDocumentExamples.Decorate(operation, path);
                if (path.StartsWith("/api/v1/notifications", StringComparison.Ordinal)) NotificationDocumentExamples.Decorate(operation, path);
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
