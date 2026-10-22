using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FaultCode.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateDriverRequest([Required] string Identifier, [Required] string OwnerObjectId,
    [Required] string DisplayName, string? Email, bool EmailNotifications);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateDriverRequest([Required] string DisplayName, string? Email, bool EmailNotifications);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleRequest([Required] string Identifier, [Required] string System);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssociationRequest(Guid DriverId);
public sealed record DriverResponse(Guid Id, string Identifier, string DisplayName, string? Email, bool EmailNotifications, bool Active, long Version);
public sealed record VehicleResponse(Guid Id, string Identifier, string System, bool Active, long Version)
{
    public IReadOnlyList<DriverResponse> Drivers { get; init; } = [];
}
public sealed record AssociationResponse(Guid Id, Guid VehicleId, Guid DriverId, DateTimeOffset From, DateTimeOffset? Until);
public sealed record MutationResponse(Guid Id, long Version);
public class FleetPageQuery
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}
public sealed class FleetQuery : FleetPageQuery
{
    public bool? Active { get; init; }
}
