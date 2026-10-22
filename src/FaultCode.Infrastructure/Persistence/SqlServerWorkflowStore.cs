using System.Data;
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
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FaultCode.Infrastructure.Persistence;

// All workflow adapters share one tenant transaction, including reentrant service calls during investigation.
public sealed class SqlServerWorkflowStore(DbContextOptions<FleetDbContext> options) : IFleetStore, IIncidentStore,
    IKnowledgeStore, IOntologyStore, IDocumentStore, IInvestigationStore, IEscalationStore, INotificationStore
{
    private sealed record Active(string Tenant, NotificationTransaction State);
    private readonly AsyncLocal<Active?> active = new();
    private async Task<T> Run<T>(string tenant, Func<NotificationTransaction, Task<T>> action, CancellationToken ct)
    {
        if (!FleetRules.CanonicalId(tenant)) throw new FleetException(403);
        if (active.Value is { } nested)
        {
            if (nested.Tenant != tenant) throw new FleetException(403);
            ct.ThrowIfCancellationRequested(); return await action(nested.State);
        }
        try
        {
            await using var db = new FleetDbContext(options);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await SqlServerFleetStore.Lock(db, tenant, "Exclusive", ct);
            var fleet = await SqlServerFleetStore.Load(db, tenant, ct);
            var oldAudit = fleet.Audit.Count; var oldReplay = fleet.Replays.Keys.ToHashSet(StringComparer.Ordinal);
            var existing = await db.WorkflowRecords.Where(r => r.TenantId == tenant).ToArrayAsync(ct);
            var state = WorkflowCodec.Decode(fleet, existing);
            active.Value = new(tenant, state);
            try
            {
                var result = await action(state); ct.ThrowIfCancellationRequested();
                if (fleet.Drivers.Any(d => d.TenantId != tenant) || fleet.Vehicles.Any(v => v.TenantId != tenant)) throw new FleetException(403);
                await SqlServerFleetStore.Save(db, fleet, tenant, oldAudit, oldReplay, ct);
                // Save fleet/incident parents before notification children; all writes remain in this transaction.
                await db.SaveChangesAsync(ct);
                var encoded = WorkflowCodec.Encode(tenant, state);
                var desired = encoded.ToDictionary(r => (r.Kind, r.Key));
                foreach (var old in existing)
                {
                    if (desired.Remove((old.Kind, old.Key), out var changed)) db.Entry(old).CurrentValues.SetValues(changed);
                    else db.WorkflowRecords.Remove(old);
                }
                foreach (var row in desired.Values.Where(r => r.Kind != "notification")) db.WorkflowRecords.Add(row);
                await db.SaveChangesAsync(ct);
                foreach (var row in desired.Values.Where(r => r.Kind == "notification")) db.WorkflowRecords.Add(row);
                await db.SaveChangesAsync(ct); ct.ThrowIfCancellationRequested();
                await transaction.CommitAsync(ct); return result;
            }
            finally { active.Value = null; }
        }
        catch (DbUpdateConcurrencyException) { throw new FleetException(409); }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 or 547 }) { throw new FleetException(409); }
        catch (DbUpdateException) { throw new FleetException(503); }
        catch (SqlException) { throw new FleetException(503); }
        catch (JsonException) { throw new FleetException(503); }
    }
    Task<FleetState> IFleetStore.ReadAsync(string tenant, CancellationToken ct) => Run(tenant, tx => Task.FromResult(tx.Fleet.Copy()), ct);
    Task<T> IFleetStore.TransactAsync<T>(string tenant, Func<FleetState, T> action, CancellationToken ct) => Run(tenant, tx => Task.FromResult(action(tx.Fleet)), ct);
    Task<IncidentState> IIncidentStore.ReadAsync(string tenant, CancellationToken ct) => Run(tenant, tx => Task.FromResult(tx.Context.Incidents.Copy()), ct);
    Task<T> IIncidentStore.TransactAsync<T>(string tenant, Func<IncidentTransaction, T> action, CancellationToken ct) => Run(tenant,
        tx => Task.FromResult(action(new(tx.Context.Incidents, () => new(tx.Fleet.Vehicles, tx.Fleet.Drivers, tx.Fleet.Associations, tx.Context.Knowledge.Faults)))), ct);
    Task<KnowledgeState> IKnowledgeStore.ReadAsync(string tenant, CancellationToken ct) => Run(tenant, tx => Task.FromResult(tx.Context.Knowledge.Copy()), ct);
    Task<T> IKnowledgeStore.TransactAsync<T>(string tenant, Func<KnowledgeState, T> action, CancellationToken ct) => Run(tenant, tx => Task.FromResult(action(tx.Context.Knowledge)), ct);
    Task<OntologyState> IOntologyStore.ReadAsync(string tenant, CancellationToken ct) => Run(tenant, tx => Task.FromResult(tx.Context.Ontology.Copy()), ct);
    Task<T> IOntologyStore.TransactAsync<T>(string tenant, Func<OntologyState, T> action, CancellationToken ct) => Run(tenant, tx => Task.FromResult(action(tx.Context.Ontology)), ct);
    Task<T> IDocumentStore.Execute<T>(string tenant, Func<DocumentState, Task<T>> action, CancellationToken ct) => Run(tenant, tx => action(tx.Context.Documents), ct);
    Task<T> IInvestigationStore.Execute<T>(string tenant, Func<InvestigationState, Task<T>> action, CancellationToken ct) => Run(tenant, tx => action(tx.Context.Investigations), ct);
    Task<T> IEscalationStore.Execute<T>(string tenant, Func<EngineeringTransaction, T> action, CancellationToken ct) => Run(tenant, tx => Task.FromResult(action(tx.Context)), ct);
    Task<T> INotificationStore.Execute<T>(string tenant, Func<NotificationTransaction, T> action, CancellationToken ct) => Run(tenant, tx => Task.FromResult(action(tx)), ct);
}
