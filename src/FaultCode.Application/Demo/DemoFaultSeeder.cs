using System.Text.Json;
using FaultCode.Application.Fleet;
using FaultCode.Application.Knowledge;
using FaultCode.Domain;
namespace FaultCode.Application.Demo;
// Explicit operator command. Never seeds on web startup or overwrites an edited definition.
public sealed class DemoFaultSeeder(IKnowledgeStore store, TimeProvider clock)
{
    public async Task<int> SeedAsync(KnowledgeCaller caller, Stream manifest, CancellationToken ct)
    {
        if (!FleetRules.CanonicalId(caller.TenantId) || !FleetRules.CanonicalId(caller.ActorId) || !FleetRules.CanonicalId(caller.ClientId)) throw new KnowledgeException(403);
        using var json = await JsonDocument.ParseAsync(manifest, cancellationToken: ct);
        if (!json.RootElement.GetProperty("demo").GetBoolean() || json.RootElement.GetProperty("schemaVersion").GetInt32() != 1) throw new KnowledgeException(400);
        var inputs = json.RootElement.GetProperty("faults").Deserialize<FaultCreate[]>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        foreach (var input in inputs) { FaultRules.Code(input.Code, input.System); FaultRules.Validate(input.Content); if (input.Content.Remedies.Any(r => r.Approved || r.Patch is not null)) throw new KnowledgeException(400); }
        return await store.TransactAsync(caller.TenantId, state =>
        {
            var count = 0;
            foreach (var input in inputs)
            {
                var code = FaultRules.Code(input.Code, input.System);
                if (state.Faults.Any(f => f.Code == code)) continue;
                var id = Guid.NewGuid(); var now = clock.GetUtcNow();
                state.Faults.Add(new(id, caller.TenantId, code, input.System, false, 1, input.Content,
                    [new(1, input.Content, "draft", caller.ActorId, now)], []));
                state.Audit.Add(new(caller.TenantId, caller.ActorId, caller.ClientId, caller.CorrelationId, "demo.faults.seed", id, 1, now)); count++;
            }
            return count;
        }, ct);
    }
}
