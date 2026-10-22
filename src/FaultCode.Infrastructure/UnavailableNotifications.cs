using FaultCode.Application.Incidents;
using FaultCode.Application.Notifications;

namespace FaultCode.Infrastructure;

public sealed class UnavailableNotificationStore : INotificationStore
{
    public Task<T> Execute<T>(string tenant, Func<NotificationTransaction, T> action, CancellationToken ct) => throw new IncidentException(503);
}
public sealed class UnavailableNotificationDelivery : INotificationDelivery
{
    public Task<DeliveryAcknowledgement> Send(LocalDelivery message, CancellationToken ct) => throw new IncidentException(503);
}
// Explicit local/test sink only. Never registered in production; no network, real addresses or secret config.
public sealed class LocalNotificationSink : INotificationDelivery
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, LocalDelivery> accepted = new();
    public IReadOnlyCollection<LocalDelivery> Accepted => accepted.Values.ToArray();
    public Task<DeliveryAcknowledgement> Send(LocalDelivery message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!FaultCode.Domain.NotificationRules.Contact(message.DemoContact)) throw new IncidentException(400);
        var prior = accepted.GetOrAdd(message.NotificationId, message);
        if (prior != message) throw new IncidentException(409);
        return Task.FromResult(new DeliveryAcknowledgement("local-" + message.NotificationId.ToString("N")));
    }
}
