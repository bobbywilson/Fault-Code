using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FaultCode.Domain;
using FaultCode.Application.Fleet;

namespace FaultCode.Application.Knowledge;

public sealed class KnowledgeService(IKnowledgeStore store, IFaultTaxonomy taxonomy, TimeProvider clock)
{
    private static void Caller(KnowledgeCaller c)
    {
        if (!FleetRules.CanonicalId(c.TenantId) || !FleetRules.CanonicalId(c.ActorId) || !FleetRules.CanonicalId(c.ClientId)
            || string.IsNullOrWhiteSpace(c.CorrelationId)) throw new KnowledgeException(403);
    }
    private static FaultDefinition Find(KnowledgeState s, KnowledgeCaller c, Guid id) => s.Faults.SingleOrDefault(f => f.Id == id && f.TenantId == c.TenantId) ?? throw new KnowledgeException(404);
    private static void Version(FaultDefinition f, long expected)
    { if (expected < 1) throw new KnowledgeException(400); if (f.Version != expected || f.Retired) throw new KnowledgeException(409); }
    private async Task Validate(KnowledgeCaller c, string system, FaultContent content, CancellationToken ct)
    {
        try { FaultRules.Validate(content); } catch (ArgumentException) { throw new KnowledgeException(400); }
        if (!await taxonomy.ValidateAsync(c.TenantId, system, content, ct)) throw new KnowledgeException(400);
    }
    private async Task<KnowledgeState> Read(KnowledgeCaller c, CancellationToken ct)
    { Caller(c); return await store.ReadAsync(c.TenantId, ct); }
    public async Task<FaultDefinition> Get(KnowledgeCaller c, Guid id, CancellationToken ct) => Find(await Read(c, ct), c, id);
    public async Task<FleetPage<FaultDefinition>> List(KnowledgeCaller c, int page, int size, string? system, string? search, bool? retired, CancellationToken ct)
    {
        if (page < 1 || size is < 1 or > 100 || system is not (null or "EPS" or "steer-by-wire" or "steering-ADAS")
            || search is not null && (search.Length > 100 || search.Any(char.IsControl))) throw new KnowledgeException(400);
        var all = (await Read(c, ct)).Faults.Where(f => f.TenantId == c.TenantId && (system is null || f.System == system)
            && (retired is null || f.Retired == retired) && (search is null || f.Code.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                || f.Draft.Description.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))).OrderBy(f => f.Code, StringComparer.Ordinal).ToArray();
        var offset = ((long)page - 1) * size;
        return new(offset >= all.Length ? [] : all.Skip((int)offset).Take(size).ToArray(), page, size, all.Length);
    }
    public async Task<MutationResult> Create(KnowledgeCaller c, FaultCreate input, string key, CancellationToken ct)
    {
        Caller(c); string code;
        try { code = FaultRules.Code(input.Code, input.System); } catch (ArgumentException) { throw new KnowledgeException(400); }
        try { FaultRules.Validate(input.Content); } catch (ArgumentException) { throw new KnowledgeException(400); }
        // Replay before dependency revalidation: return the exact committed result even during outage.
        return await Mutate(c, "faults.create", key, input with { Code = code }, async () => await Validate(c, input.System, input.Content, ct), s =>
        {
            if (s.Faults.Any(f => f.TenantId == c.TenantId && f.Code == code)) throw new KnowledgeException(409);
            var now = clock.GetUtcNow(); var id = Guid.NewGuid();
            s.Faults.Add(new(id, c.TenantId, code, input.System, false, 1, input.Content, [new(1, input.Content, "draft", c.ActorId, now)], []));
            return new(id, 1);
        }, ct);
    }
    public async Task<MutationResult> Update(KnowledgeCaller c, Guid id, FaultContent content, long version, CancellationToken ct)
    {
        var before = await Get(c, id, ct); await Validate(c, before.System, content, ct);
        return await Mutate(c, "faults.update", null, content, null, s =>
        {
            var f = Find(s, c, id); Version(f, version); var next = checked(f.Version + 1);
            s.Faults[s.Faults.IndexOf(f)] = f with { Version = next, Draft = content, History = f.History.Add(new(next, content, "draft", c.ActorId, clock.GetUtcNow())) };
            return new(id, next);
        }, ct);
    }
    public Task<MutationResult> Retire(KnowledgeCaller c, Guid id, long version, CancellationToken ct) =>
        Mutate(c, "faults.retire", null, id, null, s =>
        {
            var f = Find(s, c, id); Version(f, version); var next = checked(f.Version + 1);
            s.Faults[s.Faults.IndexOf(f)] = f with { Retired = true, Version = next, History = f.History.Add(new(next, f.Draft, "retired", c.ActorId, clock.GetUtcNow())) };
            return new(id, next);
        }, ct);
    public Task<MutationResult> Publish(KnowledgeCaller c, Guid id, long version, string key, CancellationToken ct) =>
        Mutate(c, "faults.publish/" + id, key, new { id, version }, async () =>
        { var f = await Get(c, id, ct); await Validate(c, f.System, f.Draft, ct); }, s =>
        {
            var f = Find(s, c, id); Version(f, version); var next = checked(f.Version + 1); var now = clock.GetUtcNow();
            s.Faults[s.Faults.IndexOf(f)] = f with
            {
                Version = next,
                Publications = f.Publications.Add(new(next, f.Draft, c.ActorId, now)),
                History = f.History.Add(new(next, f.Draft, "published", c.ActorId, now))
            };
            return new(id, next);
        }, ct);
    public async Task<ApprovedFault> Lookup(KnowledgeCaller c, string code, string system, string model, string software, CancellationToken ct)
    {
        string normalized;
        try { normalized = FaultRules.Code(code, system); } catch (ArgumentException) { throw new KnowledgeException(400); }
        if (!FaultRules.DemoModel(model) || !FaultRules.Software(software)) throw new KnowledgeException(400);
        var f = (await Read(c, ct)).Faults.SingleOrDefault(f => f.TenantId == c.TenantId && f.Code == normalized && !f.Retired);
        var published = f?.Publications.LastOrDefault() ?? throw new KnowledgeException(404);
        var content = published.Content with { Remedies = [.. published.Content.Remedies.Where(r => r.Approved && r.Model == model && r.SoftwareVersion == software)] };
        return new(f!.Id, f.Code, f.System, published.Version, content, published.ApprovedBy, published.ApprovedAt);
    }
    private async Task<MutationResult> Mutate(KnowledgeCaller c, string op, string? key, object payload, Func<Task>? validate, Func<KnowledgeState, MutationResult> operation, CancellationToken ct)
    {
        Caller(c);
        if (key is not null && (key.Length is < 1 or > 100 || key.Any(x => !char.IsAsciiLetterOrDigit(x) && x is not ('-' or '_')))) throw new KnowledgeException(400);
        var replayKey = JsonSerializer.Serialize(new[] { c.TenantId, c.ActorId, c.ClientId, op, key });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
        MutationResult Replay(KnowledgeReplay replay) => replay.Hash == hash ? replay.Result : throw new KnowledgeException(409);
        if (key is not null && (await Read(c, ct)).Replays.TryGetValue(replayKey, out var prior)) return Replay(prior);
        if (validate is not null) await validate();
        return await store.TransactAsync(c.TenantId, s =>
        {
            if (key is not null && s.Replays.TryGetValue(replayKey, out var existing)) return Replay(existing);
            var result = operation(s);
            s.Audit.Add(new(c.TenantId, c.ActorId, c.ClientId, c.CorrelationId, op, result.Id, result.Version, clock.GetUtcNow()));
            if (key is not null) s.Replays.Add(replayKey, new(hash, result));
            return result;
        }, ct);
    }
}
