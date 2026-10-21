using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FaultCode.Application.Fleet;
using FaultCode.Application.Knowledge;
using FaultCode.Domain;

namespace FaultCode.Application.Ontology;

public sealed class OntologyService(IOntologyStore store, TimeProvider clock)
{
    private static void Caller(KnowledgeCaller c)
    {
        if (!FleetRules.CanonicalId(c.TenantId) || !FleetRules.CanonicalId(c.ActorId) || !FleetRules.CanonicalId(c.ClientId)
            || string.IsNullOrWhiteSpace(c.CorrelationId)) throw new KnowledgeException(403);
    }
    private static T Valid<T>(Func<T> operation)
    { try { return operation(); } catch (ArgumentException) { throw new KnowledgeException(400); } }
    private static OntologyCatalog Catalog(OntologyState s) => s.Catalog ?? throw new KnowledgeException(404);
    private static void Version(OntologyCatalog c, long expected)
    { if (expected < 1) throw new KnowledgeException(400); if (c.Version != expected) throw new KnowledgeException(409); }
    private async Task<OntologyState> Read(KnowledgeCaller c, CancellationToken ct)
    { Caller(c); return await store.ReadAsync(c.TenantId, ct); }
    public async Task<OntologyCatalog> Draft(KnowledgeCaller c, CancellationToken ct) => Catalog(await Read(c, ct));
    public async Task<OntologyPublication> Published(KnowledgeCaller c, long version, CancellationToken ct)
    {
        if (version < 1) throw new KnowledgeException(400);
        return Catalog(await Read(c, ct)).Publications.SingleOrDefault(p => p.Version == version) ?? throw new KnowledgeException(404);
    }
    public async Task<OntologyEvaluation> Evaluation(KnowledgeCaller c, Guid id, CancellationToken ct) =>
        (await Read(c, ct)).Evaluations.SingleOrDefault(e => e.Id == id) ?? throw new KnowledgeException(404);
    public Task<OntologyMutation> Create(KnowledgeCaller c, OntologyContent content, string key, CancellationToken ct)
    {
        var canonical = Valid(() => OntologyRules.Canonical(content));
        return Mutate(c, "ontology.create", key, canonical, s =>
        {
            if (s.Catalog is not null) throw new KnowledgeException(409);
            var now = clock.GetUtcNow(); var id = Guid.NewGuid();
            s.Catalog = new(id, 1, canonical, [new(1, canonical, "draft", c.ActorId, now)], []);
            return new(id, 1);
        }, ct);
    }
    public Task<OntologyMutation> Update(KnowledgeCaller c, OntologyContent content, long expected, CancellationToken ct)
    {
        var canonical = Valid(() => OntologyRules.Canonical(content));
        return Mutate(c, "ontology.update", null, canonical, s =>
        {
            var catalog = Catalog(s); Version(catalog, expected);
            // Removed IDs remain reserved: concept kind/system, edge kind/endpoints and rule system are stable.
            foreach (var previous in catalog.History.Select(h => h.Content))
            {
                foreach (var old in previous.Concepts)
                    if (canonical.Concepts.SingleOrDefault(x => x.Id == old.Id) is { } next && (next.Kind != old.Kind || next.System != old.System)) throw new KnowledgeException(400);
                foreach (var old in previous.Relationships)
                    if (canonical.Relationships.SingleOrDefault(x => x.Id == old.Id) is { } next && (next.Kind != old.Kind || next.From != old.From || next.To != old.To)) throw new KnowledgeException(400);
                foreach (var old in previous.Rules)
                    if (canonical.Rules.SingleOrDefault(x => x.Id == old.Id) is { } next && next.System != old.System) throw new KnowledgeException(400);
            }
            var version = checked(catalog.Version + 1);
            s.Catalog = catalog with { Version = version, Draft = canonical, History = catalog.History.Add(new(version, canonical, "draft", c.ActorId, clock.GetUtcNow())) };
            return new(catalog.Id, version);
        }, ct);
    }
    public Task<OntologyMutation> Publish(KnowledgeCaller c, long expected, string key, CancellationToken ct) =>
        Mutate(c, "ontology.publish", key, expected, s =>
        {
            var catalog = Catalog(s); Version(catalog, expected); Valid(() => OntologyRules.Canonical(catalog.Draft));
            var next = checked(catalog.Version + 1); var now = clock.GetUtcNow();
            s.Catalog = catalog with
            {
                Version = next,
                Publications = catalog.Publications.Add(new(next, catalog.Draft, c.ActorId, now)),
                History = catalog.History.Add(new(next, catalog.Draft, "published", c.ActorId, now))
            };
            return new(catalog.Id, next);
        }, ct);
    public async Task<OntologyEvaluation> Evaluate(KnowledgeCaller c, OntologyInput input, string key, CancellationToken ct)
    {
        var canonical = Valid(() => OntologyRules.Canonical(input));
        var result = await Mutate(c, "ontology.evaluate", key, canonical, s =>
        {
            var publication = Catalog(s).Publications.SingleOrDefault(p => p.Version == canonical.PublicationVersion) ?? throw new KnowledgeException(404);
            var outcome = Valid(() => OntologyRules.Evaluate(publication.Content, canonical)); var id = Guid.NewGuid();
            s.Evaluations.Add(new(id, publication.Version, canonical, outcome, c.ActorId, clock.GetUtcNow(), publication.ApprovedBy, publication.ApprovedAt));
            return new(id, publication.Version);
        }, ct);
        return await Evaluation(c, result.Id, ct);
    }
    private async Task<OntologyMutation> Mutate(KnowledgeCaller c, string op, string? key, object payload,
        Func<OntologyState, OntologyMutation> operation, CancellationToken ct)
    {
        Caller(c);
        if (key is not null && (key.Length is < 1 or > 100 || key.Any(x => !char.IsAsciiLetterOrDigit(x) && x is not ('-' or '_')))) throw new KnowledgeException(400);
        // POST callers always supply a non-null key; update alone has no replay requirement.
        if (op != "ontology.update" && key is null) throw new KnowledgeException(400);
        var scope = JsonSerializer.Serialize(new[] { c.TenantId, c.ActorId, c.ClientId, op, key });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
        OntologyMutation Replay(OntologyReplay replay) => replay.Hash == hash ? replay.Result : throw new KnowledgeException(409);
        return await store.TransactAsync(c.TenantId, s =>
        {
            if (key is not null && s.Replays.TryGetValue(scope, out var previous)) return Replay(previous);
            var result = operation(s);
            s.Audit.Add(new(c.ActorId, c.ClientId, c.CorrelationId, op, result.Id, result.Version, clock.GetUtcNow()));
            if (key is not null) s.Replays.Add(scope, new(hash, result));
            return result;
        }, ct);
    }
}
