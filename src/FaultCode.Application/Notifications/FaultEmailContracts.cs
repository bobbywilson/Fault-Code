using FaultCode.Application.Knowledge;
namespace FaultCode.Application.Notifications;

public sealed record FaultIndexHit(Guid Id, long Version);
public interface IFaultCodeIndex
{
    Task<FaultIndexHit?> Find(string tenant, string code, string system, string model, string software, CancellationToken ct);
    Task Upsert(string tenant, ApprovedFault fault, CancellationToken ct);
}
public sealed record FaultEmailFacts(string Code, string Summary, string Action, Guid? FaultId, long PublicationVersion);
public sealed record IncidentResolutionDocument(Guid IncidentId, string Code, string System, string Model, string SoftwareVersion,
    long Version, string Resolution);
public interface IIncidentResolutionIndex
{
    Task UpsertResolution(string tenant, IncidentResolutionDocument document, CancellationToken ct);
    Task<IncidentResolutionDocument?> FindResolution(string tenant, Guid incidentId, CancellationToken ct);
}
public delegate Task<string> FaultEmailTool(string name, Dictionary<string, object> arguments, CancellationToken ct);
public interface IFaultEmailAgent
{
    Task Run(FaultEmailTool tool, CancellationToken ct);
}
