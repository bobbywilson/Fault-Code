using System.Net.Http.Json;
using System.Text.Json;
using FaultCode.Api.Configuration;
using FaultCode.Application.Incidents;
using FaultCode.Application.Notifications;
using FaultCode.Infrastructure;
using Xunit;

namespace FaultCode.IntegrationTests;

public sealed class MailpitFactAttribute : FactAttribute
{
    public MailpitFactAttribute()
    { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FAULTCODE_MAILPIT_TEST_URL"))) Skip = "Real Mailpit capture endpoint not configured."; }
}
public sealed class MailpitDeliveryTests
{
    [Fact]
    public void Capture_configuration_and_recipient_rules_remain_private_and_demo_only()
    {
        Assert.True(new NotificationOptions { Provider = "Mailpit" }.Valid);
        Assert.True(new NotificationOptions { Provider = "Mailpit", Endpoint = "http://mailpit:8025/" }.Valid);
        Assert.False(new NotificationOptions { Provider = "Mailpit", Endpoint = "http://example.com:8025/" }.Valid);
        Assert.False(new NotificationOptions { Provider = "Mailpit", Endpoint = "http://127.0.0.1:8025/?secret=x" }.Valid);
        Assert.True(MailpitNotificationDelivery.DemoEmail("alex.morgan@example.com"));
        Assert.False(MailpitNotificationDelivery.DemoEmail("person@customer.invalid"));
        Assert.False(MailpitNotificationDelivery.DemoEmail("alex@example.com\r\nBcc: other@example.com"));
    }
    [Fact]
    public async Task Capture_rejects_non_demo_recipient_before_any_network_call()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:1/") };
        var delivery = new MailpitNotificationDelivery(http);
        var error = await Assert.ThrowsAsync<IncidentException>(() => delivery.Send(new(Guid.NewGuid(), "person@customer.invalid", "A test"), default));
        Assert.Equal(400, error.Status);
    }
    [MailpitFact]
    public async Task Real_mailpit_accepts_and_retains_recipient_body_and_notification_reference()
    {
        var endpoint = Environment.GetEnvironmentVariable("FAULTCODE_MAILPIT_TEST_URL")!;
        Assert.True(new NotificationOptions { Provider = "Mailpit", Endpoint = endpoint }.Valid);
        using var http = new HttpClient { BaseAddress = new Uri(endpoint), Timeout = TimeSpan.FromSeconds(15) };
        var delivery = new MailpitNotificationDelivery(http); var id = Guid.NewGuid();
        const string body = "We received your vehicle fault report and are investigating. No diagnosis has been confirmed.";
        var receipt = await delivery.Send(new(id, "alex.morgan@example.com", body), default);
        Assert.StartsWith("mailpit-", receipt.Receipt);
        using var response = await http.GetAsync("api/v1/message/" + receipt.Receipt[8..]);
        response.EnsureSuccessStatusCode();
        using var message = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("alex.morgan@example.com", message.RootElement.GetRawText());
        Assert.Equal(body, message.RootElement.GetProperty("Text").GetString());
        // Captured by actual Mailpit. Not a model-generated or externally delivered email claim.
    }
}
