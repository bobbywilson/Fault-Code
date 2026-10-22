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
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync(ct);
            var resource = $"faultcode-quota-{tenantId}-{identityHash}-{day:yyyyMMdd}";
            await using var command = connection.CreateCommand();
            // One short server-side transaction: no query compilation or application round trips
            // while this identity/day lock is held. XACT_ABORT rolls back errors/cancellation.
            command.CommandText = """
                SET NOCOUNT ON;
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;
                DECLARE @result int, @used int;
                EXEC @result = sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=3000;
                IF @result < 0 THROW 51000, 'Quota reservation unavailable.', 1;
                SELECT @used = Used FROM dbo.ApiRequestQuotas WHERE TenantId=@tenant AND IdentityHash=@identity AND Day=@day;
                IF @used IS NULL
                BEGIN
                    INSERT dbo.ApiRequestQuotas (TenantId, IdentityHash, Day, Used) VALUES (@tenant,@identity,@day,1);
                    SET @used=1;
                END
                ELSE IF @used < @limit
                BEGIN
                    UPDATE dbo.ApiRequestQuotas SET Used=Used+1 WHERE TenantId=@tenant AND IdentityHash=@identity AND Day=@day;
                    SET @used=@used+1;
                END
                ELSE SET @used=NULL;
                COMMIT;
                SELECT @used;
                """;
            command.Parameters.Add(new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = resource });
            command.Parameters.Add(new SqlParameter("@tenant", SqlDbType.NVarChar, 36) { Value = tenantId });
            command.Parameters.Add(new SqlParameter("@identity", SqlDbType.NVarChar, 64) { Value = identityHash });
            command.Parameters.Add(new SqlParameter("@day", SqlDbType.Date) { Value = day.ToDateTime(TimeOnly.MinValue) });
            command.Parameters.Add(new SqlParameter("@limit", SqlDbType.Int) { Value = limit });
            return await command.ExecuteScalarAsync(ct) is int used ? used : null;
        }
        catch (SqlException error) { throw new ApiQuotaUnavailableException(error.Number); }
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
