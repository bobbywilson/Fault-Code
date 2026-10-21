using FaultCode.Application.Telemetry;
using FaultCode.Infrastructure.Telemetry;
using FaultCode.Api.Mcp;
using FaultCode.Api.Configuration;
using FaultCode.Api.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using FaultCode.Application;
using FaultCode.Infrastructure;
using Microsoft.Extensions.Options;
using FaultCode.Api.Identity;
using FaultCode.Api.Authorization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOptions<TelemetryOptions>().Configure<IConfiguration>((o, configuration) =>
{
    var section = configuration.GetSection("Telemetry");
    o.Provider = section["Provider"] ?? "None"; o.Endpoint = section["Endpoint"] ?? "";
    var ratio = section["TraceSampleRatio"];
    o.TraceSampleRatio = ratio is null ? 0.1 : double.TryParse(ratio, System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : double.NaN;
})
    .Validate(o => o.Valid, "Telemetry:Provider, Endpoint or TraceSampleRatio is invalid.").ValidateOnStart();
builder.Services.AddSingleton<ITelemetryAdapter>(s =>
{
    var o = s.GetRequiredService<IOptions<TelemetryOptions>>().Value;
    return o.Provider switch
    {
        "Local" => new LocalTelemetryAdapter(s.GetRequiredService<ILogger<LocalTelemetryAdapter>>()),
        "Otlp" or "Datadog" => new OtlpTelemetryAdapter(new(o.Endpoint, o.TraceSampleRatio,
            s.GetRequiredService<IOptions<AgentOptions>>().Value is { Enabled: true } agent ? agent.Model : null)),
        _ => new NoOpTelemetryAdapter()
    };
});
builder.Services.AddSingleton<ITelemetry, SafeTelemetry>();
builder.Services.AddEntraAuthentication(builder.Configuration);
builder.Services.AddFaultCodeAuthorization();
builder.Services.AddFaultCodeMcp(builder.Configuration);
builder.Services.AddOptions<AgentOptions>().Bind(builder.Configuration.GetSection("Agents"))
    .Validate(o => o.Valid, "Agents configuration requires bounded limits and, when enabled, an explicit model snapshot and ApiKey.").ValidateOnStart();
builder.Services.AddTransient<SanitizedHttpTelemetryHandler>();
builder.Services.AddHttpClient("agent-runtime").RemoveAllLoggers().AddHttpMessageHandler<SanitizedHttpTelemetryHandler>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddTransient<FaultCode.Application.Agents.IAgentProvider>(s =>
{
    var o = s.GetRequiredService<IOptions<AgentOptions>>().Value;
    var http = s.GetRequiredService<IHttpClientFactory>().CreateClient("agent-runtime");
    http.Timeout = Timeout.InfiniteTimeSpan;
    return new AgentFrameworkProvider(http, o.Enabled, o.Model, o.ApiKey);
});
builder.Services.AddSingleton(s => s.GetRequiredService<IOptions<AgentOptions>>().Value.Limits);
builder.Services.AddScoped<FaultCode.Application.Agents.AgentRuntimeService>();
builder.Services.AddSingleton<FaultCode.Application.Investigations.IInvestigationStore, UnavailableInvestigationStore>();
builder.Services.AddScoped<FaultCode.Application.Investigations.InvestigationService>();
builder.Services.AddSingleton<FaultCode.Application.Escalations.IEscalationStore, UnavailableEscalationStore>();
builder.Services.AddScoped<FaultCode.Application.Escalations.EscalationService>();
builder.Services.AddSingleton<FaultCode.Application.Notifications.INotificationStore, UnavailableNotificationStore>();
builder.Services.AddSingleton<FaultCode.Application.Notifications.INotificationDelivery, UnavailableNotificationDelivery>();
builder.Services.AddScoped<FaultCode.Application.Notifications.NotificationService>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<FaultCode.Application.Fleet.IFleetStore, UnavailableFleetStore>();
builder.Services.AddScoped<FaultCode.Application.Fleet.FleetService>();
builder.Services.AddSingleton<FaultCode.Application.Knowledge.IKnowledgeStore, UnavailableKnowledgeStore>();
builder.Services.AddSingleton<FaultCode.Application.Ontology.IOntologyStore, UnavailableOntologyStore>();
builder.Services.AddSingleton<FaultCode.Application.Knowledge.IFaultTaxonomy, FaultCode.Application.Ontology.ApprovedOntologyTaxonomy>();
builder.Services.AddScoped<FaultCode.Application.Ontology.OntologyService>();
builder.Services.AddScoped<FaultCode.Application.Knowledge.KnowledgeService>();
builder.Services.AddSingleton<FaultCode.Application.Incidents.IIncidentStore, UnavailableIncidentStore>();
builder.Services.AddScoped<FaultCode.Application.Incidents.IncidentService>();

builder.Services.AddSingleton<FaultCode.Application.Retrieval.IDocumentStore, UnavailableDocumentStore>();
builder.Services.AddScoped<FaultCode.Application.Retrieval.RetrievalService>();
builder.Services.AddOptions<EmbeddingOptions>().Bind(builder.Configuration.GetSection("Embeddings"))
    .Validate(o => !o.Enabled || (!string.IsNullOrWhiteSpace(o.ApiKey) && !o.ApiKey.Any(char.IsControl)
        && !string.IsNullOrWhiteSpace(o.Model) && o.Model.Length <= 100
        && o.Model.All(x => char.IsAsciiLetterOrDigit(x) || x is '-' or '_' or '.') && o.Dimensions is >= 1 and <= 4096),
        "Embeddings:Model, Dimensions and ApiKey must be explicitly configured when enabled.").ValidateOnStart();
builder.Services.AddHttpClient("embeddings").RemoveAllLoggers().AddHttpMessageHandler<SanitizedHttpTelemetryHandler>().ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient("document-index").RemoveAllLoggers().AddHttpMessageHandler<SanitizedHttpTelemetryHandler>().ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddTransient<FaultCode.Application.Retrieval.IEmbeddingProvider>(s =>
{
    var o = s.GetRequiredService<IOptions<EmbeddingOptions>>().Value;
    return new EmbeddingHttpProvider(s.GetRequiredService<IHttpClientFactory>().CreateClient("embeddings"), new(o.Model, o.Dimensions), o.ApiKey, o.Enabled);
});
builder.Services.AddTransient<FaultCode.Application.Retrieval.IDocumentIndex>(s =>
{
    var o = s.GetRequiredService<IOptions<QdrantOptions>>().Value;
    return new QdrantDocumentIndex(s.GetRequiredService<IHttpClientFactory>().CreateClient("document-index"), o.Endpoint, o.ApiKey, o.Enabled);
});

builder.Services.AddOptions<FoundationOptions>()
    .Bind(builder.Configuration.GetSection(FoundationOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApplicationName)
        && options.ApplicationName.Length <= 100,
        "FaultCode:ApplicationName must contain 1-100 characters and cannot be blank; set FaultCode__ApplicationName.")
    .ValidateOnStart();
builder.Services.AddSingleton<IRuntimeEnvironment>(new RuntimeEnvironment(builder.Environment.EnvironmentName));
builder.Services.AddSingleton(provider => new FoundationStatusService(
    provider.GetRequiredService<IOptions<FoundationOptions>>().Value.ApplicationName,
    provider.GetRequiredService<IRuntimeEnvironment>()));
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(context.ModelState)
        {
            Status = 400,
            Title = "Invalid request",
            Type = "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
            Instance = context.HttpContext.Request.Path
        };
        // Binding errors may contain raw supplied values; retain field names only.
        foreach (var key in problem.Errors.Keys.ToArray()) problem.Errors[key] = ["Invalid value."];
        problem.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(problem)
        { ContentTypes = { "application/problem+json" } };
    };
});
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance = context.HttpContext.Request.Path;
    context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
    context.ProblemDetails.Detail = null;
    context.ProblemDetails.Title ??= Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(context.ProblemDetails.Status ?? 500);
    context.ProblemDetails.Type ??= "about:blank";
});
builder.Services.AddOpenApi("v1", options => options.AddDocumentTransformer<FaultCode.Api.Contracts.ApiDocumentTransformer>());
builder.Services.AddOptions<QdrantOptions>()
    .Bind(builder.Configuration.GetSection("Qdrant"))
    .Validate(options => !options.Enabled || (!string.IsNullOrWhiteSpace(options.ApiKey)
        && !options.ApiKey.Any(char.IsControl)),
        "Qdrant:ApiKey is required when enabled; set Qdrant__ApiKey.")
    .Validate(options => !options.Enabled || (Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var uri)
        && (uri.Scheme == "http" || uri.Scheme == "https") && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)),
        "Qdrant:Endpoint must be an absolute HTTP(S) URL without credentials or query.")
    .ValidateOnStart();
builder.Services.AddHttpClient<QdrantReadinessCheck>()
    .RemoveAllLoggers();
builder.Services.AddHealthChecks().AddCheck<QdrantReadinessCheck>("qdrant", tags: ["ready"]);

var app = builder.Build();
app.Use(async (context, next) =>
{
    context.TraceIdentifier = Guid.NewGuid().ToString("N");
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
        return Task.CompletedTask;
    });
    var telemetry = context.RequestServices.GetRequiredService<ITelemetry>();
    using var operation = telemetry.Begin(TelemetryOperation.Api, context.TraceIdentifier);
    try
    {
        await next(context);
        operation.Complete(context.Response.StatusCode >= 500 ? TelemetryOutcome.Failed
            : context.Response.StatusCode >= 400 ? TelemetryOutcome.Rejected : TelemetryOutcome.Success);
    }
    catch (OperationCanceledException) { operation.Report(TelemetryError.Cancelled); operation.Complete(TelemetryOutcome.Cancelled); throw; }
    catch { operation.Report(TelemetryError.Unexpected); throw; }
});
app.UseExceptionHandler(handler => handler.Run(context => WriteProblem(context, 500)));
app.UseStatusCodePages(context => WriteProblem(context.HttpContext, context.HttpContext.Response.StatusCode));
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (FaultCode.Application.Agents.AgentFailure error) when (!context.Response.HasStarted)
    { await WriteProblem(context, error.Status, error.Code); }
    catch (FaultCode.Application.Incidents.IncidentException error) when (!context.Response.HasStarted)
    { await WriteProblem(context, error.Status); }
    catch (FaultCode.Application.Knowledge.KnowledgeException error) when (!context.Response.HasStarted)
    { await WriteProblem(context, error.Status, error.FailureCode); }
    catch (FaultCode.Application.Fleet.FleetException error) when (!context.Response.HasStarted)
    { await WriteProblem(context, error.Status); }
});
app.UseAuthentication();
app.UseAuthorization();
app.Use(McpTransport.ValidateBody);
app.MapMcp("/api/v1/mcp").RequireAuthorization("mcp.access").ExcludeFromDescription();
app.MapOpenApi().AllowAnonymous();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
app.Run();

static Task WriteProblem(HttpContext context, int status, string? failureCode = null)
{
    var extensions = new Dictionary<string, object?> { ["correlationId"] = context.TraceIdentifier };
    if (failureCode is "document-storage-unavailable" or "embedding-unavailable" or "vector-index-unavailable"
        or "collection-incompatible" or "embedding-profile-invalid" or "embedding-response-invalid" or "approved-evidence-unindexed" or "agent-not-configured" or "agent-rate-limited" or "agent-timeout"
        or "agent-invalid-output" or "agent-refused" or "agent-provider-invalid" or "agent-budget-exhausted"
        or "agent-tool-budget-exhausted" or "agent-provider-unavailable" or "agent-provider-rejected")
        extensions["failureCode"] = failureCode;
    return Results.Problem(statusCode: status,
        title: Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(status), type: "about:blank",
        instance: context.Request.Path, extensions: extensions).ExecuteAsync(context);
}

public partial class Program;
