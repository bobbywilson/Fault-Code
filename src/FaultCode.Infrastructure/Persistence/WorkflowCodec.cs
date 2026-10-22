using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FaultCode.Application.Escalations;
using FaultCode.Application.Fleet;
using FaultCode.Application.Incidents;
using FaultCode.Application.Investigations;
using FaultCode.Application.Knowledge;
using FaultCode.Application.Notifications;
using FaultCode.Application.Ontology;
using FaultCode.Application.Retrieval;
using FaultCode.Domain;

namespace FaultCode.Infrastructure.Persistence;

internal static class WorkflowCodec
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 64 };
    internal static NotificationTransaction Empty(FleetState fleet) => new(new(), fleet, new(new(), new(), new(), new(), new(), new()));
    internal static IReadOnlyList<WorkflowRecord> Encode(string tenant, NotificationTransaction state)
    {
        var rows = new List<WorkflowRecord>();
        void Add<T>(string kind, string key, T value, int position)
        {
            var payload = JsonSerializer.Serialize(value, Json);
            using var document = JsonDocument.Parse(payload);
            var row = new WorkflowRecord { TenantId = tenant, Kind = kind, Key = key, Payload = payload, Position = position,
                Version = document.RootElement.TryGetProperty("version", out var version) && version.TryGetInt64(out var v) ? v : 0 };
            if (value is Incident incident)
            {
                if (incident.TenantId != tenant) throw new IncidentException(403);
                row.VehicleId = incident.Fleet.VehicleId; row.DriverId = incident.Fleet.DriverId;
                row.SourceClientId = incident.SourceClientId; row.SourceEventId = incident.SourceEventId;
            }
            if (value is DriverNotification notification)
            {
                if (notification.TenantId != tenant) throw new IncidentException(403);
                row.DriverId = notification.DriverId; row.ParentKind = "incident"; row.ParentKey = notification.IncidentId.ToString("D");
            }
            rows.Add(row);
        }
        void List<T>(string kind, IReadOnlyList<T> values, Func<T, string>? key = null)
        { for (var i = 0; i < values.Count; i++) Add(kind, key?.Invoke(values[i]) ?? i.ToString(CultureInfo.InvariantCulture), values[i], i); }
        void Map<T>(string kind, IReadOnlyDictionary<string, T> values) =>
            List(kind, values.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray(), x => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(x.Key)))) ;
        var c = state.Context;
        List("incident", c.Incidents.Incidents, x => x.Id.ToString("D"));
        List("incident-audit", c.Incidents.Audit); Map("incident-replay", c.Incidents.Replays); Map("incident-source", c.Incidents.Sources);
        List("fault", c.Knowledge.Faults, x => x.Id.ToString("D")); List("knowledge-audit", c.Knowledge.Audit); Map("knowledge-replay", c.Knowledge.Replays);
        if (c.Ontology.Catalog is not null) Add("ontology", "catalog", c.Ontology.Catalog, 0);
        List("ontology-evaluation", c.Ontology.Evaluations, x => x.Id.ToString("D")); List("ontology-audit", c.Ontology.Audit); Map("ontology-replay", c.Ontology.Replays);
        List("document", c.Documents.Documents, x => x.Id.ToString("D")); List("document-audit", c.Documents.Audit); Map("document-replay", c.Documents.Replays);
        List("investigation", c.Investigations.Results, x => x.Result.Id.ToString("D")); List("investigation-audit", c.Investigations.Audit); Map("investigation-replay", c.Investigations.Replays);
        List("engineering-case", c.Engineering.Cases, x => x.Id.ToString("D")); List("engineering-event", c.Engineering.Events, x => x.Id.ToString("D"));
        List("engineering-audit", c.Engineering.Audit); Map("engineering-replay", c.Engineering.Replays);
        List("notification", state.Notifications.Messages, x => x.Id.ToString("D")); List("notification-preference", state.Notifications.Preferences, x => x.DriverId.ToString("D"));
        List("notification-audit", state.Notifications.Audit); Map("notification-replay", state.Notifications.Replays);
        return rows;
    }
    internal static NotificationTransaction Decode(FleetState fleet, IReadOnlyList<WorkflowRecord> rows)
    {
        if (rows.Any(r => r.SchemaVersion != 1)) throw new JsonException("Unsupported workflow schema.");
        var state = Empty(fleet); var c = state.Context; var known = new HashSet<string>(StringComparer.Ordinal);
        T[] Read<T>(string kind)
        {
            known.Add(kind);
            return rows.Where(r => r.Kind == kind).OrderBy(r => r.Position).ThenBy(r => r.Key, StringComparer.Ordinal)
                .Select(r => JsonSerializer.Deserialize<T>(r.Payload, Json) ?? throw new JsonException("Invalid workflow record.")).ToArray();
        }
        void List<T>(string kind, List<T> target) => target.AddRange(Read<T>(kind));
        void Map<T>(string kind, Dictionary<string, T> target)
        { foreach (var pair in Read<KeyValuePair<string, T>>(kind)) target.Add(pair.Key, pair.Value); }
        List("incident", c.Incidents.Incidents); List("incident-audit", c.Incidents.Audit); Map("incident-replay", c.Incidents.Replays); Map("incident-source", c.Incidents.Sources);
        List("fault", c.Knowledge.Faults); List("knowledge-audit", c.Knowledge.Audit); Map("knowledge-replay", c.Knowledge.Replays);
        c.Ontology.Catalog = Read<OntologyCatalog>("ontology").SingleOrDefault();
        List("ontology-evaluation", c.Ontology.Evaluations); List("ontology-audit", c.Ontology.Audit); Map("ontology-replay", c.Ontology.Replays);
        List("document", c.Documents.Documents); List("document-audit", c.Documents.Audit); Map("document-replay", c.Documents.Replays);
        List("investigation", c.Investigations.Results); List("investigation-audit", c.Investigations.Audit); Map("investigation-replay", c.Investigations.Replays);
        List("engineering-case", c.Engineering.Cases); List("engineering-event", c.Engineering.Events); List("engineering-audit", c.Engineering.Audit); Map("engineering-replay", c.Engineering.Replays);
        List("notification", state.Notifications.Messages); List("notification-preference", state.Notifications.Preferences); List("notification-audit", state.Notifications.Audit); Map("notification-replay", state.Notifications.Replays);
        if (rows.Any(r => !known.Contains(r.Kind))) throw new JsonException("Unsupported workflow record kind.");
        return state;
    }
}
