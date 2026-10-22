namespace FaultCode.Application.Ontology;

public sealed record DomainRelationship(string From, string Relationship, string To, string Detail, string? Route, string Source);

// Versioned application-owned reference model; no tenant mutation or publication is exposed.
public static class DomainOntology
{
    public const string Version = "1";
    public static IReadOnlyList<DomainRelationship> Relationships { get; } = Array.AsReadOnly(new DomainRelationship[]
    {
        new("Fault code", "is reported in", "Incident", "An occurrence records the fault code, steering system, model and software version.", "incidents", "Incident.Code/System; IncidentKnowledge.DefinitionId"),
        new("Incident", "occurs on", "Vehicle", "The incident retains the vehicle identity and its fleet snapshot.", "vehicles", "Incident.Fleet.VehicleId"),
        new("Incident", "involves", "Driver", "The incident retains the driver identity and vehicle association at intake.", "drivers", "Incident.Fleet.DriverId/Association"),
        new("Driver", "is assigned to", "Vehicle", "An association records when a driver’s assignment starts and ends.", "vehicles", "Association.VehicleId/DriverId/From/Until"),
        new("Incident", "has", "Notification", "A notification links its incident to an approved remedy and delivery history.", "notifications", "DriverNotification.IncidentId"),
        new("Notification", "is addressed to", "Driver", "Delivery follows the driver’s notification preferences and approved contact.", "drivers", "DriverNotification.DriverId; LocalNotificationPreference.DriverId"),
        new("Notification", "concerns", "Fault code", "The message retains the exact fault publication and ontology version used.", null, "DriverNotification.FaultId/PublicationVersion/OntologyVersion"),
        new("Fault code", "has approved", "Classification", "Fault knowledge references an exact approved classification and version.", null, "FaultContent.ClassificationId/ClassificationVersion"),
        new("Fault code", "describes", "Symptoms", "Symptoms describe the observed steering behavior associated with the fault.", null, "FaultContent.Symptoms"),
        new("Fault code", "has", "Remedy", "Versioned fault knowledge contains remedy guidance and approval status.", null, "FaultContent.Remedies; FaultRemedy.Approved"),
        new("Incident", "may escalate to", "Engineering case", "An engineering case links the incident and investigation to resolution review.", null, "EngineeringCase.IncidentId/InvestigationId"),
    });
}
