using FaultCode.Api.Configuration;
using FaultCode.Api.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using FaultCode.Application;
using FaultCode.Infrastructure;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddControllers();
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
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.Run();

public partial class Program;
