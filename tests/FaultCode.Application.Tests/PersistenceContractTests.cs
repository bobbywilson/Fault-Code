using FaultCode.Application.Persistence;
using FaultCode.Domain;

namespace FaultCode.Application.Tests;

public sealed class PersistenceContractTests
{
    [Fact]
    public void Repository_contract_stays_typed_and_cancellable_without_provider_dependencies()
    {
        var repository = typeof(IRepository<SyntheticAggregate>);
        Assert.Equal(typeof(Task<SyntheticAggregate>), repository.GetMethod("FindAsync")!.ReturnType);
        Assert.Equal(new[] { typeof(Guid), typeof(CancellationToken) },
            repository.GetMethod("FindAsync")!.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(typeof(Task), repository.GetMethod("AddAsync")!.ReturnType);
        Assert.Equal(new[] { typeof(SyntheticAggregate), typeof(CancellationToken) },
            repository.GetMethod("AddAsync")!.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(typeof(Task<int>), typeof(IUnitOfWork).GetMethod("SaveChangesAsync")!.ReturnType);
        Assert.Equal(typeof(CancellationToken), Assert.Single(typeof(IUnitOfWork).GetMethod("SaveChangesAsync")!.GetParameters()).ParameterType);
    }

    private sealed record SyntheticAggregate(Guid Id) : IAggregateRoot;
}
