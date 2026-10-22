using System.Collections.Concurrent;
using System.Data;
using FaultCode.Application;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FaultCode.Infrastructure.Persistence;

public sealed class SqlServerApiRequestQuotaStore(DbContextOptions<FleetDbContext> options) : IApiRequestQuotaStore
{
    public async Task<int?> Consume(string tenantId, string identityHash, DateOnly day, int limit, CancellationToken ct)
    {
        try
        {
            await using var db = new FleetDbContext(options);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            var resource = $"faultcode-quota-{tenantId}-{identityHash}-{day:yyyyMMdd}";
            // Serialize just this identity/day across API instances and process restarts.
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=3000; IF @result < 0 THROW 51000, 'Quota reservation unavailable.', 1;", ct);
            var row = await db.ApiRequestQuotas.FindAsync([tenantId, identityHash, day], ct);
            if (row is not null && row.Used >= limit) { await tx.CommitAsync(ct); return null; }
            if (row is null)
            {
                row = new() { TenantId = tenantId, IdentityHash = identityHash, Day = day };
                db.ApiRequestQuotas.Add(row);
            }
            row.Used++;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return row.Used;
        }
        catch (SqlException) { throw new ApiQuotaUnavailableException(); }
        catch (DbUpdateException) { throw new ApiQuotaUnavailableException(); }
    }
}

// Development only; production never falls back to a counter that resets on restart.
public sealed class DevelopmentApiRequestQuotaStore : IApiRequestQuotaStore
{
    private sealed class Counter { public int Used; }
    private readonly ConcurrentDictionary<(string, string, DateOnly), Counter> counters = new();
    public Task<int?> Consume(string tenantId, string identityHash, DateOnly day, int limit, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var counter = counters.GetOrAdd((tenantId, identityHash, day), _ => new());
        lock (counter)
            return Task.FromResult<int?>(counter.Used >= limit ? null : ++counter.Used);
    }
}

public sealed class UnavailableApiRequestQuotaStore : IApiRequestQuotaStore
{
    public Task<int?> Consume(string tenantId, string identityHash, DateOnly day, int limit, CancellationToken ct)
        => throw new ApiQuotaUnavailableException();
}
