using System.Collections.Immutable;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FaultCode.Application.Knowledge;
using FaultCode.Application.Retrieval;
namespace FaultCode.Infrastructure;

public sealed class EmbeddingHttpProvider(HttpClient client, IndexProfile profile, string? apiKey, bool enabled) : IEmbeddingProvider
{
    public IndexProfile Profile => profile;
    public async Task<ImmutableArray<float>> Embed(string text, CancellationToken ct)
    {
        profile.Validate();
        if (!enabled || string.IsNullOrWhiteSpace(apiKey) || apiKey.Any(char.IsControl)) throw new KnowledgeException(503, "embedding-unavailable");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = JsonContent.Create(new { input = text, model = profile.Model, dimensions = profile.Dimensions, encoding_format = "float" });
            using var response = await client.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode) throw new KnowledgeException(503, "embedding-unavailable");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var root = json.RootElement; var data = root.GetProperty("data");
            if (root.GetProperty("model").GetString() != profile.Model || data.GetArrayLength() != 1 || data[0].GetProperty("index").GetInt32() != 0) throw new KnowledgeException(503, "embedding-unavailable");
            var vector = data[0].GetProperty("embedding").EnumerateArray().Select(x => x.GetSingle()).ToImmutableArray();
            VectorRules.Validate(vector, profile); return vector;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { throw new KnowledgeException(503, "embedding-unavailable"); }
    }
}
