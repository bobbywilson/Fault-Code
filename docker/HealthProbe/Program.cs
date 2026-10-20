// Container-local probe: never write URLs, response bodies or credentials to logs.
using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
try
{
    using var request = new HttpRequestMessage(HttpMethod.Get, args[0]);
    var key = Environment.GetEnvironmentVariable("QDRANT__SERVICE__API_KEY");
    if (!string.IsNullOrWhiteSpace(key))
    {
        request.Headers.Add("api-key", key);
    }
    using var response = await client.SendAsync(request);
    return response.IsSuccessStatusCode ? 0 : 1;
}
catch
{
    return 1;
}
