namespace FaultCode.Application.Persistence;

/// <summary>Commits staged repository changes. No storage provider is selected by this contract.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
