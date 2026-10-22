namespace FaultCode.Application;

// A successful reservation is committed before endpoint work starts. No bearer token is stored.
public interface IApiRequestQuotaStore
{
    Task<int?> Consume(string tenantId, string identityHash, DateOnly day, int limit, CancellationToken ct);
}

public sealed class ApiQuotaUnavailableException : Exception;
