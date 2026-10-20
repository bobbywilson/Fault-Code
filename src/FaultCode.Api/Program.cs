using FaultCode.Api.Configuration;
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
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapControllers();
app.MapHealthChecks("/health/live");
app.Run();

public partial class Program;
