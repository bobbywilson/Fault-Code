using FaultCode.Api.Configuration;
using FaultCode.Application.Notifications;
using FaultCode.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FaultCode.Api.Controllers;

public sealed record CapabilityHealth(string Component, string Status, string Detail);
public sealed record OperationalHealthResponse(bool Ready, DateTimeOffset CapturedAt, CapabilityHealth[] Capabilities);

[ApiController]
[Route("api/v1/admin/health")]
public sealed class OperationalHealthController(IServiceProvider services, HealthCheckService health,
    IOptions<AgentOptions> agents, IOptions<EmbeddingOptions> embeddings, IOptions<QdrantOptions> qdrant,
    IOptions<TelemetryOptions> telemetry, INotificationDelivery delivery, TimeProvider clock) : ControllerBase
{
    [HttpGet(Name = "GetOperationalHealth"), Authorize(Policy = "integrations.manage")]
    [ProducesResponseType<OperationalHealthResponse>(200)]
    public async Task<OperationalHealthResponse> Get(CancellationToken ct)
    {
        // Configuration evidence is not a durable-storage or external-ingestion health claim.
        object[] stores = [services.GetRequiredService<FaultCode.Application.Fleet.IFleetStore>(),
            services.GetRequiredService<FaultCode.Application.Knowledge.IKnowledgeStore>(),
            services.GetRequiredService<FaultCode.Application.Incidents.IIncidentStore>(),
            services.GetRequiredService<FaultCode.Application.Ontology.IOntologyStore>(),
            services.GetRequiredService<FaultCode.Application.Retrieval.IDocumentStore>(),
            services.GetRequiredService<FaultCode.Application.Investigations.IInvestigationStore>(),
            services.GetRequiredService<FaultCode.Application.Escalations.IEscalationStore>(),
            services.GetRequiredService<INotificationStore>(),
            services.GetRequiredService<FaultCode.Application.Administration.IAdministrationStore>()];
        var unavailable = stores.Any(s => s.GetType().Name.StartsWith("Unavailable", StringComparison.Ordinal));
        var infrastructure = await health.CheckHealthAsync(ct);
        CapabilityHealth[] capabilities = [
            new("transactional-storage", unavailable ? "not-configured" : "unverified", "Durability and restart acceptance must pass before business readiness."),
            new("participation-consent", "not-implemented", "Invitation and vehicle-registration consent are not implemented."),
            new("email-delivery", delivery is UnavailableNotificationDelivery ? "not-configured" : "unverified", "A configured transport alone does not prove consent or accepted delivery."),
            new("qdrant", !qdrant.Value.Enabled ? "disabled" : infrastructure.Status == HealthStatus.Healthy ? "healthy" : "unhealthy", "Infrastructure connectivity only; canonical documents and retrieval acceptance are separate."),
            new("agent", agents.Value.Enabled ? "configured-unverified" : "disabled", "Live model and authorized MCP execution require separate verification."),
            new("embeddings", embeddings.Value.Enabled ? "configured-unverified" : "disabled", "Actual indexing and retrieval are not implied by configuration."),
            new("telemetry", telemetry.Value.Provider == "None" ? "disabled" : "configured-unverified", "Actual provider ingestion must be verified independently.")];
        return new(capabilities.All(c => c.Status == "healthy"), clock.GetUtcNow(), capabilities);
    }
}
