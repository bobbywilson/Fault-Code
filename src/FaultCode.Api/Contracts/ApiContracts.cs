using System.ComponentModel.DataAnnotations;

namespace FaultCode.Api.Contracts;

public sealed record HealthResponse(string Status);
public sealed record ExampleResponse(string Id, string System, string Description);
public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed class ExampleQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;
    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
    [RegularExpression("^(EPS|steer-by-wire|steering-ADAS)$")]
    public string? System { get; init; }
}
