using FaultCode.Domain;

namespace FaultCode.Application.Persistence;

/// <summary>Provider-independent aggregate access; durability is controlled by the unit of work.</summary>
public interface IRepository<TAggregate> where TAggregate : class, IAggregateRoot
{
    Task<TAggregate?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
}
