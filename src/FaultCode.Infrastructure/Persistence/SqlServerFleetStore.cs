using System.Data;
using System.Security.Cryptography;
using System.Text;
using FaultCode.Application.Fleet;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FaultCode.Infrastructure.Persistence;

public sealed class SqlServerFleetStore(DbContextOptions<FleetDbContext> options) : IFleetStore
{
    private static void Tenant(string value) { if (!FaultCode.Domain.FleetRules.CanonicalId(value)) throw new FleetException(403); }
    public async Task<FleetState> ReadAsync(string tenantId, CancellationToken ct)
    {
        Tenant(tenantId);
        try
        {
            await using var db = new FleetDbContext(options);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await Lock(db, tenantId, "Shared", ct);
            var state = await Load(db, tenantId, ct); await tx.CommitAsync(ct); return state;
        }
        catch (SqlException) { throw new FleetException(503); }
    }
    public async Task<T> TransactAsync<T>(string tenantId, Func<FleetState, T> operation, CancellationToken ct)
    {
        Tenant(tenantId);
        try
        {
            await using var db = new FleetDbContext(options);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await Lock(db, tenantId, "Exclusive", ct);
            var state = await Load(db, tenantId, ct);
            var audits = state.Audit.Count; var replayKeys = state.Replays.Keys.ToHashSet(StringComparer.Ordinal);
            var result = operation(state); ct.ThrowIfCancellationRequested();
            if (state.Drivers.Any(d => d.TenantId != tenantId) || state.Vehicles.Any(v => v.TenantId != tenantId)
                || state.Audit.Any(a => a.TenantId != tenantId)) throw new FleetException(403);
            await Save(db, state, tenantId, audits, replayKeys, ct);
            await db.SaveChangesAsync(ct); ct.ThrowIfCancellationRequested(); await tx.CommitAsync(ct); return result;
        }
        catch (DbUpdateConcurrencyException) { throw new FleetException(409); }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 or 547 }) { throw new FleetException(409); }
        catch (DbUpdateException) { throw new FleetException(503); }
        catch (SqlException) { throw new FleetException(503); }
    }
    internal static async Task Save(FleetDbContext db, FleetState state, string tenantId, int audits, HashSet<string> replayKeys, CancellationToken ct)
    {
        // Keep rows normalized and tenant-bound; never replace or delete unrelated records.
        foreach (var driver in state.Drivers) await Upsert(db, driver, tenantId, driver.Id, ct);
        foreach (var vehicle in state.Vehicles) await Upsert(db, vehicle, tenantId, vehicle.Id, ct);
        foreach (var a in state.Associations)
            await Upsert(db, new FleetAssociationRow
            {
                TenantId = tenantId,
                Id = a.Id,
                VehicleId = a.VehicleId,
                DriverId = a.DriverId,
                From = a.From,
                Until = a.Until
            }, tenantId, a.Id, ct);
        foreach (var a in state.Audit.Skip(audits)) db.Audit.Add(new()
        {
            TenantId = tenantId,
            ActorId = a.ActorId,
            ClientId = a.ClientId,
            CorrelationId = a.CorrelationId,
            Operation = a.Operation,
            ResourceId = a.ResourceId,
            At = a.At
        });
        foreach (var (key, value) in state.Replays.Where(x => !replayKeys.Contains(x.Key)))
            db.Replays.Add(new()
            {
                TenantId = tenantId,
                KeyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))),
                Key = key,
                PayloadHash = value.PayloadHash,
                ResourceId = value.Result.Id,
                Version = value.Result.Version
            });
    }
    private static async Task Upsert<TEntity>(FleetDbContext db, TEntity row, string tenantId, Guid id, CancellationToken ct) where TEntity : class
    {
        var existing = await db.Set<TEntity>().FindAsync([tenantId, id], ct);
        if (existing is null) db.Add(row); else db.Entry(existing).CurrentValues.SetValues(row);
    }
    internal static Task Lock(FleetDbContext db, string tenantId, string mode, CancellationToken ct)
    {
        var resource = "faultcode-fleet-" + tenantId;
        return db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode={mode}, @LockOwner='Transaction', @LockTimeout=15000; IF @result < 0 THROW 51000, 'Fleet transaction lock unavailable.', 1;", ct);
    }
    internal static async Task<FleetState> Load(FleetDbContext db, string tenantId, CancellationToken ct)
    {
        var state = new FleetState();
        state.Drivers.AddRange(await db.Drivers.AsNoTracking().Where(x => x.TenantId == tenantId).ToArrayAsync(ct));
        state.Vehicles.AddRange(await db.Vehicles.AsNoTracking().Where(x => x.TenantId == tenantId).ToArrayAsync(ct));
        state.Associations.AddRange((await db.Associations.AsNoTracking().Where(x => x.TenantId == tenantId).ToArrayAsync(ct))
            .Select(x => new FaultCode.Domain.Association(x.Id, x.VehicleId, x.DriverId, x.From, x.Until)));
        state.Audit.AddRange((await db.Audit.AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.Id).ToArrayAsync(ct))
            .Select(x => new FleetAudit(x.TenantId, x.ActorId, x.ClientId, x.CorrelationId, x.Operation, x.ResourceId, x.At)));
        foreach (var r in await db.Replays.AsNoTracking().Where(x => x.TenantId == tenantId).ToArrayAsync(ct))
            state.Replays.Add(r.Key, new(r.PayloadHash, new(r.ResourceId, r.Version)));
        return state;
    }
}
