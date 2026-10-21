using FaultCode.Application.Administration;
using FaultCode.Application.Fleet;

namespace FaultCode.Infrastructure;

public sealed class UnavailableAdministrationStore : IAdministrationStore
{
    public Task<AdministrationPage<ManagedUser>> Users(AdministrationAudit caller, int page, int pageSize, CancellationToken ct) => throw new FleetException(503);
    public Task<ManagedUser> User(AdministrationAudit caller, Guid id, CancellationToken ct) => throw new FleetException(503);
    public Task<ManagedUser> ReplaceUser(AdministrationAudit caller, Guid id, string[] roles, long version, string key, CancellationToken ct) => throw new FleetException(503);
    public Task<AdministrationPage<ManagedIntegration>> Integrations(AdministrationAudit caller, int page, int pageSize, CancellationToken ct) => throw new FleetException(503);
    public Task<ManagedIntegration> Integration(AdministrationAudit caller, Guid id, CancellationToken ct) => throw new FleetException(503);
    public Task<ManagedIntegration> ReplaceIntegration(AdministrationAudit caller, Guid id, bool enabled, long version, string key, CancellationToken ct) => throw new FleetException(503);
}
