using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FaultCode.Application.Knowledge;
using FaultCode.Application.Retrieval;
namespace FaultCode.Infrastructure;

public sealed class QdrantDocumentIndex(HttpClient client, string endpoint, string? apiKey, bool enabled) : IDocumentIndex
{
    private static string Collection(IndexProfile p) => "faultcode_documents_" + p.Signature;
    private async Task<JsonElement> Send(HttpMethod method, string path, object? body, CancellationToken ct, bool missing = false)
    {
        if (!enabled || string.IsNullOrWhiteSpace(apiKey)) throw new KnowledgeException(503, "vector-index-unavailable");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var request = new HttpRequestMessage(method, new Uri(new Uri(endpoint), path)); request.Headers.Add("api-key", apiKey);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await client.SendAsync(request, timeout.Token);
            if (missing && response.StatusCode == HttpStatusCode.NotFound) return default;
            if (!response.IsSuccessStatusCode) throw new KnowledgeException(503, "vector-index-unavailable");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var root = json.RootElement;
            if (!root.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.String || status.GetString() != "ok"
                || !root.TryGetProperty("result", out var result) || result.ValueKind is JsonValueKind.Null or JsonValueKind.False) throw new KnowledgeException(503, "vector-index-unavailable");
            return root.Clone();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or FormatException)
        { throw new KnowledgeException(503, "vector-index-unavailable"); }
    }
    public async Task Ensure(IndexProfile profile, CancellationToken ct)
    {
        profile.Validate(); var path = "/collections/" + Collection(profile);
        var json = await Send(HttpMethod.Get, path, null, ct, true);
        if (json.ValueKind == JsonValueKind.Undefined)
        {
            // Concurrent first creation can conflict: reread authoritative config regardless of create outcome.
            try { await Send(HttpMethod.Put, path, new { vectors = new { size = profile.Dimensions, distance = "Cosine" } }, ct); }
            catch (KnowledgeException) { }
            json = await Send(HttpMethod.Get, path, null, ct);
        }
        try
        {
            var vectors = json.GetProperty("result").GetProperty("config").GetProperty("params").GetProperty("vectors");
            if (vectors.GetProperty("size").GetInt32() != profile.Dimensions || vectors.GetProperty("distance").GetString() != "Cosine") throw new KnowledgeException(503, "collection-incompatible");
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException) { throw new KnowledgeException(503, "vector-index-unavailable"); }
    }
    private static object Match(string key, object value) => new { key, match = new { value } };
    public async Task Upsert(string tenant, IndexProfile profile, ImmutableArray<VectorPoint> points, CancellationToken ct)
    {
        foreach (var p in points) VectorRules.Validate(p.Vector, profile);
        var result = await Send(HttpMethod.Put, "/collections/" + Collection(profile) + "/points?wait=true", new
        {
            points = points.Select(p => new
            {
                id = p.Id,
                vector = p.Vector,
                payload = new
                {
                    tenant,
                    revision = p.RevisionKey,
                    system = p.System,
                    model = p.Model,
                    software = p.SoftwareVersion,
                    access = p.Access,
                    publisher = p.Publisher,
                    document = p.DocumentId,
                    approved = true,
                    profile = profile.Signature
                }
            })
        }, ct);
        Completed(result);
    }
    public async Task Delete(string tenant, IndexProfile profile, Guid? documentId, CancellationToken ct)
    {
        var conditions = new List<object> { Match("tenant", tenant) }; if (documentId.HasValue) conditions.Add(Match("document", documentId.Value));
        var result = await Send(HttpMethod.Post, "/collections/" + Collection(profile) + "/points/delete?wait=true", new { filter = new { must = conditions } }, ct);
        Completed(result);
    }
    private static void Completed(JsonElement json)
    {
        var result = json.GetProperty("result");
        if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty("status", out var status)
            || status.ValueKind != JsonValueKind.String || status.GetString() != "completed") throw new KnowledgeException(503, "vector-index-unavailable");
    }
    public async Task<ImmutableArray<VectorHit>> Search(string tenant, string actor, IndexProfile profile, EvidenceQuery query,
        ImmutableArray<string> allowedRevisions, ImmutableArray<float> vector, CancellationToken ct)
    {
        VectorRules.Validate(vector, profile); if (allowedRevisions.IsDefaultOrEmpty) return [];
        var json = await Send(HttpMethod.Post, "/collections/" + Collection(profile) + "/points/query", new
        {
            query = vector,
            limit = query.Limit,
            score_threshold = 0.75,
            with_payload = false,
            with_vector = false,
            filter = new
            {
                must = new object[] { Match("tenant", tenant), Match("approved", true), Match("profile", profile.Signature),
                    Match("system", query.System), Match("model", query.Model), Match("software", query.SoftwareVersion),
                    new { key = "revision", match = new { any = allowedRevisions } },
                    new { should = new[] { Match("access", "tenant"), Match("publisher", actor) } } }
            }
        }, ct);
        try { return [.. json.GetProperty("result").GetProperty("points").EnumerateArray().Select(x => new VectorHit(x.GetProperty("id").GetGuid(), x.GetProperty("score").GetDouble()))]; }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException) { throw new KnowledgeException(503, "vector-index-unavailable"); }
    }
}
