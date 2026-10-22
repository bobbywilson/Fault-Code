using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FaultCode.Application.Incidents;
using FaultCode.Application.Notifications;

namespace FaultCode.Infrastructure;

// Real capture transport. Mailpit must have no relay configured; this is not Microsoft365 delivery.
// No automatic retries: an uncertain HTTP acknowledgement requires durable dispatcher reconciliation.
public sealed class MailpitNotificationDelivery(HttpClient http) : INotificationDelivery
{
    public static bool DemoEmail(string? value) => value is { Length: <= 150 }
        && Regex.IsMatch(value, "^[a-zA-Z0-9._+-]+@example\\.com$", RegexOptions.CultureInvariant);
    public async Task<DeliveryAcknowledgement> Send(LocalDelivery message, CancellationToken ct)
    {
        if (message.NotificationId == Guid.Empty || !DemoEmail(message.DemoContact)
            || string.IsNullOrWhiteSpace(message.Message) || message.Message.Length > 16000) throw new IncidentException(400);
        var id = message.NotificationId.ToString("N");
        using var response = await http.PostAsJsonAsync("api/v1/send", new
        {
            From = new { Email = "faultcode@example.com", Name = "Fault Code" },
            To = new[] { new { Email = message.DemoContact } },
            Subject = "Vehicle fault update",
            Text = message.Message,
            Headers = new Dictionary<string, string> { ["Message-ID"] = $"<{id}@faultcode.example.com>", ["X-FaultCode-Notification-Id"] = id },
            Tags = new[] { "Fault Code", "Demo" }
        }, ct);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 8192) throw new IncidentException(503);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!payload.RootElement.TryGetProperty("ID", out var receipt) || receipt.GetString() is not { Length: > 0 and <= 100 } value
            || !value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')) throw new IncidentException(503);
        return new("mailpit-" + value);
    }
}
