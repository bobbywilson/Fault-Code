using FaultCode.Api.Configuration;
using FaultCode.Api.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using FaultCode.Application;
using FaultCode.Infrastructure;
using Microsoft.Extensions.Options;
using FaultCode.Api.Identity;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEntraAuthentication(builder.Configuration);

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
    await next(context);
});
app.UseExceptionHandler(handler => handler.Run(context => WriteProblem(context, 500)));
app.UseStatusCodePages(context => WriteProblem(context.HttpContext, context.HttpContext.Response.StatusCode));
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi().AllowAnonymous();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
app.Run();

static Task WriteProblem(HttpContext context, int status) => Results.Problem(
    statusCode: status,
    title: Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(status),
    type: "about:blank",
    instance: context.Request.Path,
    extensions: new Dictionary<string, object?> { ["correlationId"] = context.TraceIdentifier })
    .ExecuteAsync(context);

public partial class Program;
