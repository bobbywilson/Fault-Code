using System.Text.Json;
using FaultCode.Application.Fleet;
using FaultCode.Application.Notifications;
using ModelContextProtocol.Protocol;
namespace FaultCode.Api.Mcp;

public sealed record NotificationToolContext(Guid NotificationId, Guid Lease);
public sealed class FaultEmailWorkflow(FaultEmailService service, IFaultEmailAgent agent, McpTools tools, IHttpContextAccessor accessor)
{
    public async Task Process(FleetCaller caller, Guid id, CancellationToken ct)
    {
        var lease = await service.Acquire(caller, id, ct); if (lease is null) return;
        var http = accessor.HttpContext ?? throw new InvalidOperationException();
        http.Items[typeof(NotificationToolContext)] = new NotificationToolContext(id, lease.Value);
        try
        {
            await service.Prepare(caller, id, lease.Value, ct);
            await agent.Run(async (name, args, token) =>
            {
                var result = await tools.Call(new CallToolRequestParams { Name = name, Arguments = args.ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value)) }, token);
                return result.Content.OfType<TextContentBlock>().Single().Text;
            }, ct);
        }
        finally
        {
            http.Items.Remove(typeof(NotificationToolContext));
            await service.Fail(caller, id, lease.Value, CancellationToken.None);
        }
    }
}
