using FaultCode.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaultCode.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/examples")]
[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(500, "application/problem+json")]
public sealed class ExamplesController : ControllerBase
{
    private static readonly ExampleResponse[] Examples =
    [
        new("synthetic-001", "EPS", "Synthetic electric power steering example"),
        new("synthetic-002", "steer-by-wire", "Synthetic steer-by-wire example"),
        new("synthetic-003", "steering-ADAS", "Synthetic steering ADAS example")
    ];

    [HttpGet(Name = "ListExamples")]
    [ProducesResponseType<PageResponse<ExampleResponse>>(200)]
    public ActionResult<PageResponse<ExampleResponse>> List([FromQuery] ExampleQuery query)
    {
        var matches = Examples.Where(x => query.System is null || x.System == query.System).ToArray();
        var offset = ((long)query.Page - 1) * query.PageSize;
        var items = offset >= matches.Length ? [] : matches.Skip((int)offset).Take(query.PageSize).ToArray();
        return Ok(new PageResponse<ExampleResponse>(items, query.Page, query.PageSize, matches.Length));
    }

    [HttpGet("{id}", Name = "GetExample")]
    [ProducesResponseType<ExampleResponse>(200)]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
    public ActionResult<ExampleResponse> Get(string id)
    {
        var item = Examples.SingleOrDefault(x => x.Id == id);
        return item is null ? NotFound() : Ok(item);
    }
}
