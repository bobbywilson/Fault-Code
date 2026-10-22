using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FaultCode.Application.Incidents;
using FaultCode.Domain;

namespace FaultCode.Infrastructure;

// Keys stay in this server-side adapter, outside model/MCP arguments and logs.
public sealed class DatadogIncidentTracker(HttpClient http, string accessToken) : IIncidentTracker
{
    public async Task<TrackedIncident> Publish(Incident incident, string? externalId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new IncidentTrackingException("datadog-not-configured");
        if (externalId is not null && !Guid.TryParse(externalId, out _)) throw new IncidentTrackingException("datadog-invalid-reference");
        var title = $"Fault Code {incident.Id:D}: {incident.Code}";
        var resolved = incident.Resolution is not null;
        var attributes = new
        {
            title,
            customer_impacted = false,
            is_test = true,
            notification_handles = Array.Empty<object>(),
            fields = new { state = new { type = "dropdown", value = resolved ? "resolved" : "active" } }
        };
        object body = externalId is null
            ? new { data = new { type = "incidents", attributes, relationships = new { commander_user = new { data = (object?)null } } } }
            : new { data = new { type = "incidents", id = externalId, attributes = new { attributes.fields } } };
        using var request = new HttpRequestMessage(externalId is null ? HttpMethod.Post : HttpMethod.Patch,
            "https://api.datadoghq.com/api/v2/incidents" + (externalId is null ? "" : "/" + externalId));
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Content = JsonContent.Create(body);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode)
            {
                var code = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? "datadog-access-denied" : "datadog-request-failed";
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    var error = await response.Content.ReadAsStringAsync(deadline.Token);
                    if (error.Length <= 4096)
                    {
                        try
                        {
                            using var problem = JsonDocument.Parse(error);
                            if (problem.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array
                                && errors.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("title", out var title) && title.GetString() == "Seat Required"))
                                code = "datadog-seat-required";
                        }
                        catch (JsonException) { }
                    }
                }
                throw new IncidentTrackingException(code, (int)response.StatusCode >= 500);
            }
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: deadline.Token);
            var data = json.RootElement.GetProperty("data"); var id = data.GetProperty("id").GetString();
            var a = data.GetProperty("attributes"); var number = a.GetProperty("public_id").GetInt64();
            if (data.GetProperty("type").GetString() != "incidents" || !Guid.TryParse(id, out var parsed) || parsed == Guid.Empty
                || externalId is not null && id != externalId || number < 1 || a.GetProperty("title").GetString() != title)
                throw new IncidentTrackingException("datadog-invalid-receipt", true);
            return new(id!, number, "https://app.datadoghq.com/incidents/" + number.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new IncidentTrackingException("datadog-acceptance-unknown", true);
        }
    }
}
