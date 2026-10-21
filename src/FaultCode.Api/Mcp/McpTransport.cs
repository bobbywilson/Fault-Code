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
        services.AddMcpServer(o => o.ServerInfo = new() { Name = "Fault Code", Version = "1.0.0" })
            .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.Stateless)
            .WithListToolsHandler((context, ct) => context.Services!.GetRequiredService<McpTools>().List(ct))
            .WithCallToolHandler((context, ct) => context.Services!.GetRequiredService<McpTools>().Call(context.Params, ct));
        services.AddLogging(o => o.AddFilter("ModelContextProtocol", LogLevel.None));
        return services;
    }
    public static async Task ValidateBody(HttpContext context, RequestDelegate next)
    {
        if (!string.Equals(context.Request.Path.Value?.TrimEnd('/'), "/api/v1/mcp", StringComparison.OrdinalIgnoreCase) || !HttpMethods.IsPost(context.Request.Method)) { await next(context); return; }
        // This transport serves authenticated non-browser consumers. No browser origins are enabled.
        if (context.Request.Headers.ContainsKey("Origin"))
        {
            await Results.Problem(statusCode: 403, title: "Forbidden", instance: context.Request.Path,
                extensions: new Dictionary<string, object?> { ["correlationId"] = context.TraceIdentifier }).ExecuteAsync(context);
            return;
        }
        // Read no more than limit+1, including bodies without Content-Length. No buffering to disk.
        var buffer = new byte[65537]; var count = 0;
        while (count < buffer.Length)
        {
            var read = await context.Request.Body.ReadAsync(buffer.AsMemory(count), context.RequestAborted);
            if (read == 0) break;
            count += read;
        }
        var valid = count <= 65536;
        if (valid)
        {
            try
            {
                using var document = JsonDocument.Parse(buffer.AsMemory(0, count), new() { MaxDepth = 16 });
                valid = document.RootElement.ValueKind == JsonValueKind.Object && Unique(document.RootElement);
            }
            catch (JsonException) { valid = false; }
        }
        if (!valid)
        {
            await Results.Problem(statusCode: 400, title: "Invalid request", instance: context.Request.Path,
                extensions: new Dictionary<string, object?> { ["correlationId"] = context.TraceIdentifier }).ExecuteAsync(context);
            return;
        }
        using var body = new MemoryStream(buffer, 0, count, writable: false);
        context.Request.Body = body;
        await next(context);
    }
    private static bool Unique(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in e.EnumerateObject()) if (!names.Add(p.Name) || !Unique(p.Value)) return false;
        }
        if (e.ValueKind == JsonValueKind.Array) foreach (var child in e.EnumerateArray()) if (!Unique(child)) return false;
        return true;
    }
}
