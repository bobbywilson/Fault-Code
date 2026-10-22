using FaultCode.Domain;

namespace FaultCode.Domain.Tests;

public sealed class FleetRulesTests
{
    [Theory]
    [InlineData(" syn-abc-001 ", "SYN-", "SYN-ABC-001")]
    [InlineData(" drv-001 ", "DRV-", "DRV-001")]
    public void Normalizes_demo_identifiers(string input, string prefix, string expected) => Assert.Equal(expected, FleetRules.Identifier(input, prefix));
    [Theory]
    [InlineData("", "SYN-")]
    [InlineData("real-vin", "SYN-")]
    [InlineData("SYN-", "SYN-")]
    [InlineData("DRV-X!", "DRV-")]
    public void Rejects_invalid_identifiers(string input, string prefix) => Assert.Throws<ArgumentException>(() => FleetRules.Identifier(input, prefix));
    [Fact]
    public void Association_requires_active_same_tenant_owned_entities_and_unique_active_pair()
    {
        const string tenant = "11111111-1111-4111-8111-111111111111";
        var d = new Driver(Guid.NewGuid(), tenant, "DRV-001", tenant, "Demo", null, false, true, 1);
        var v = new Vehicle(Guid.NewGuid(), tenant, "SYN-001", "EPS", true, 1);
        var a = new Association(Guid.NewGuid(), v.Id, d.Id, DateTimeOffset.UtcNow, null);
        Assert.True(FleetRules.CanAssociate(v, d, []));
        Assert.False(FleetRules.CanAssociate(v with { Active = false }, d, []));
        Assert.False(FleetRules.CanAssociate(v, d with { Active = false }, []));
        Assert.False(FleetRules.CanAssociate(v, d with { TenantId = "other" }, []));
        Assert.False(FleetRules.CanAssociate(v, d with { OwnerObjectId = "" }, []));
        Assert.False(FleetRules.CanAssociate(v, d, [a]));
        Assert.True(FleetRules.CanAssociate(v, d, [a with { Until = DateTimeOffset.UtcNow }]));
        Assert.True(FleetRules.CanAssociate(v, d with { Id = Guid.NewGuid() }, [a]));
        Assert.True(FleetRules.CanAssociate(v with { Id = Guid.NewGuid() }, d, [a]));
    }
}
