using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FaultCode.Application.Knowledge;
using FaultCode.Application.Notifications;
using FaultCode.Application.Retrieval;
namespace FaultCode.Infrastructure;
// Exact payload filters establish identity; embeddings support retrieval, never prove a code match.
public sealed class QdrantFaultCodeIndex(HttpClient http, string endpoint, string? key, bool enabled, IEmbeddingProvider embeddings) : IFaultCodeIndex, IIncidentResolutionIndex
{
    private string Collection => "faultcode_faults_" + embeddings.Profile.Signature;
    private async Task<JsonElement> Send(HttpMethod method, string path, object? body, CancellationToken ct, bool missing = false)
    {
        if (!enabled || string.IsNullOrWhiteSpace(key)) throw new KnowledgeException(503, "fault-index-unavailable");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var request = new HttpRequestMessage(method, new Uri(new Uri(endpoint), path)); request.Headers.Add("api-key", key);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, timeout.Token);
            if (missing && response.StatusCode == HttpStatusCode.NotFound) return default;
            if (!response.IsSuccessStatusCode) throw new KnowledgeException(503, "fault-index-unavailable");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            if (json.RootElement.GetProperty("status").GetString() != "ok") throw new KnowledgeException(503);
            return json.RootElement.GetProperty("result").Clone();
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or OperationCanceledException && !ct.IsCancellationRequested) { throw new KnowledgeException(503, "fault-index-unavailable"); }
    }
    public async Task Upsert(string tenant, ApprovedFault fault, CancellationToken ct)
    {
        var path = "/collections/" + Collection;
        var config = await Send(HttpMethod.Get, path, null, ct, true);
        if (config.ValueKind == JsonValueKind.Undefined) { await Send(HttpMethod.Put, path, new { vectors = new { size = embeddings.Profile.Dimensions, distance = "Cosine" } }, ct); config = await Send(HttpMethod.Get, path, null, ct); }
        if (config.GetProperty("config").GetProperty("params").GetProperty("vectors").GetProperty("size").GetInt32() != embeddings.Profile.Dimensions) throw new KnowledgeException(503);
        var vector = await embeddings.Embed(fault.Code + " " + fault.Content.Description, ct);
        var result = await Send(HttpMethod.Put, path + "/points?wait=true", new
        {
            points = new[] { new { id = fault.Id, vector,
            payload = new { tenant, code = fault.Code, system = fault.System, model = "SYN-STEERING-DEMO", software = "demo-1", version = fault.Version, approved = true } } }
        }, ct);
        if (result.GetProperty("status").GetString() != "completed") throw new KnowledgeException(503);
    }
    public async Task<FaultIndexHit?> Find(string tenant, string code, string system, string model, string software, CancellationToken ct)
    {
        object Match(string key, object value) => new { key, match = new { value } };
        var result = await Send(HttpMethod.Post, "/collections/" + Collection + "/points/scroll", new
        {
            filter = new
            {
                must = new[] {
            Match("tenant", tenant), Match("code", code), Match("system", system), Match("model", model), Match("software", software), Match("approved", true) }
            },
            limit = 2,
            with_payload = true,
            with_vector = false
        }, ct);
        var points = result.GetProperty("points");
        if (points.GetArrayLength() == 0) return null;
        if (points.GetArrayLength() != 1) throw new KnowledgeException(503);
        return new(points[0].GetProperty("id").GetGuid(), points[0].GetProperty("payload").GetProperty("version").GetInt64());
    }
    // Separate case evidence from the approved catalog. Resolving a case does not
    // publish an OEM diagnosis, approve a remedy, or broaden vehicle applicability.
    public async Task UpsertResolution(string tenant, IncidentResolutionDocument document, CancellationToken ct)
    {
        var path = "/collections/" + Collection + "_resolutions";
        var config = await Send(HttpMethod.Get, path, null, ct, true);
        if (config.ValueKind == JsonValueKind.Undefined)
        {
            await Send(HttpMethod.Put, path, new { vectors = new { size = embeddings.Profile.Dimensions, distance = "Cosine" } }, ct);
            config = await Send(HttpMethod.Get, path, null, ct);
        }
        if (config.GetProperty("config").GetProperty("params").GetProperty("vectors").GetProperty("size").GetInt32() != embeddings.Profile.Dimensions) throw new KnowledgeException(503);
        var vector = await embeddings.Embed(document.Code + "\n" + document.Resolution, ct);
        var result = await Send(HttpMethod.Put, path + "/points?wait=true", new { points = new[] { new { id = document.IncidentId, vector, payload = new { tenant, document } } } }, ct);
        if (result.GetProperty("status").GetString() != "completed") throw new KnowledgeException(503);
    }
    public async Task<IncidentResolutionDocument?> FindResolution(string tenant, Guid incidentId, CancellationToken ct)
    {
        var result = await Send(HttpMethod.Post, "/collections/" + Collection + "_resolutions/points/scroll", new
        {
            filter = new { must = new object[] { new { key = "tenant", match = new { value = tenant } }, new { key = "document.incidentId", match = new { value = incidentId.ToString("D") } } } },
            limit = 2,
            with_payload = true,
            with_vector = false
        }, ct, true);
        if (result.ValueKind == JsonValueKind.Undefined) return null;
        var points = result.GetProperty("points"); if (points.GetArrayLength() == 0) return null;
        if (points.GetArrayLength() != 1) throw new KnowledgeException(503, "resolution-index-stale");
        return points[0].GetProperty("payload").GetProperty("document").Deserialize<IncidentResolutionDocument>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
