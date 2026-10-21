namespace FaultCode.Domain;

public sealed record Driver(Guid Id, string TenantId, string Identifier, string OwnerObjectId,
    string DisplayName, string? Email, bool EmailNotifications, bool Active, long Version) : IAggregateRoot;
public sealed record Vehicle(Guid Id, string TenantId, string Identifier, string System, bool Active, long Version) : IAggregateRoot;
public sealed record Association(Guid Id, Guid VehicleId, Guid DriverId, DateTimeOffset From, DateTimeOffset? Until);

public static class FleetRules
{
    public static bool CanonicalId(string? value) => Guid.TryParseExact(value, "D", out var id)
        && id != Guid.Empty && id.ToString("D") == value;
    public static string Identifier(string? value, string prefix)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? "";
        if (!normalized.StartsWith(prefix, StringComparison.Ordinal) || normalized.Length <= prefix.Length
            || normalized.Length > 40 || normalized.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Invalid synthetic identifier.");
        return normalized;
    }
    public static bool CanAssociate(Vehicle vehicle, Driver driver, IEnumerable<Association> history) =>
        vehicle.Active && driver.Active && CanonicalId(driver.OwnerObjectId) && vehicle.TenantId == driver.TenantId
        && !history.Any(a => a.VehicleId == vehicle.Id && a.Until is null);
}
