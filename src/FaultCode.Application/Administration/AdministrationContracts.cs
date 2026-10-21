using FaultCode.Application.Fleet;

namespace FaultCode.Application.Administration;

public sealed record ManagedUser(Guid Id, string DisplayName, string[] Roles, long Version);
public sealed record ManagedIntegration(Guid Id, string Name, string Provider, bool Enabled, long Version);
public sealed record AdministrationPage<T>(T[] Items, int Page, int PageSize, long TotalCount);
public sealed record AdministrationAudit(string Tenant, string Actor, string Client, string CorrelationId);

// Implementations must tenant-filter before count/page and atomically commit metadata,
// audit and actor/client/operation/key/payload replay. Never provision external grants.
public interface IAdministrationStore
{
    Task<AdministrationPage<ManagedUser>> Users(AdministrationAudit caller, int page, int pageSize, CancellationToken ct);
    Task<ManagedUser> User(AdministrationAudit caller, Guid id, CancellationToken ct);
    Task<ManagedUser> ReplaceUser(AdministrationAudit caller, Guid id, string[] roles, long version, string key, CancellationToken ct);
    Task<AdministrationPage<ManagedIntegration>> Integrations(AdministrationAudit caller, int page, int pageSize, CancellationToken ct);
    Task<ManagedIntegration> Integration(AdministrationAudit caller, Guid id, CancellationToken ct);
    Task<ManagedIntegration> ReplaceIntegration(AdministrationAudit caller, Guid id, bool enabled, long version, string key, CancellationToken ct);
}
