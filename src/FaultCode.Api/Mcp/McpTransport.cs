using System.Text.Json;
using ModelContextProtocol.AspNetCore;

namespace FaultCode.Api.Mcp;

public static class McpTransport
{
    public static IServiceCollection AddFaultCodeMcp(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<McpOptions>().Bind(configuration.GetSection("Mcp"))
            .Validate(o => o.TimeoutSeconds is >= 1 and <= 120, "Mcp:TimeoutSeconds must be 1-120.").ValidateOnStart();
        services.AddScoped<McpTools>();
        services.AddMcpServer(o => { o.ServerInfo = new() { Name = "Fault Code", Version = "1.1.0" }; o.ServerInstructions = "Fault Code investigates vehicle-reported steering codes. For a server-bound notification, first call notification_fault_lookup: exact Qdrant payload matching is revalidated against approved SQL knowledge; failure is never an unknown diagnosis. Use notification_email_send with exactly the returned summary and action and friendly/formal tone. The server selects the consenting demo driver and private Mailpit destination, records the message, delivery attempts and receipts, and emits OpenTelemetry for Datadog. Never invent fault meanings or approved repairs, send arbitrary recipients, expose credentials or deploy vehicle software. A resolved incident requires an explicitly authorized API status change and a separate follow-up notification. Datadog completion requires real provider ingestion evidence, not a tool success alone."; })
            .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.Stateless)
            .WithListToolsHandler((context, ct) => context.Services!.GetRequiredService<McpTools>().List(ct))
            .WithCallToolHandler((context, ct) => context.Services!.GetRequiredService<McpTools>().Call(context.Params, ct));
        services.AddLogging(o => o.AddFilter("ModelContextProtocol", LogLevel.None));
        return services;
    }
}
