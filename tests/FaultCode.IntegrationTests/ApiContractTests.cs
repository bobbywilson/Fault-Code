using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FaultCode.IntegrationTests;

public sealed class ApiContractTests
{
    [Fact]
    public async Task Public_versioned_health_and_examples_have_stable_contracts()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var health = JsonDocument.Parse(await client.GetStringAsync("/api/v1/health"));
        Assert.Equal("Healthy", health.RootElement.GetProperty("status").GetString());
        using var list = JsonDocument.Parse(await client.GetStringAsync("/api/v1/examples"));
        var root = list.RootElement;
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(new[] { "synthetic-001", "synthetic-002", "synthetic-003" },
            root.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetString()));
        using var item = JsonDocument.Parse(await client.GetStringAsync("/api/v1/examples/synthetic-001"));
        Assert.Equal("EPS", item.RootElement.GetProperty("system").GetString());
    }

    [Theory]
    [InlineData("?page=1&pageSize=1", 1, 3)]
    [InlineData("?page=2&pageSize=1", 1, 3)]
    [InlineData("?page=2147483647&pageSize=100", 0, 3)]
    [InlineData("?system=EPS&pageSize=100", 1, 1)]
    [InlineData("?system=steer-by-wire", 1, 1)]
    [InlineData("?system=steering-ADAS&page=2", 0, 1)]
    public async Task Pagination_and_filtering_are_bounded_and_accurate(string query, int count, int total)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/v1/examples" + query));
        Assert.Equal(count, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(total, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=-1")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?page=2147483648")]
    [InlineData("?page=secret-supplied-value")]
    [InlineData("?system=secret-supplied-value")]
    public async Task Invalid_queries_return_safe_validation_problems(string query)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/examples" + query);
        var json = await AssertProblem(response, 400);
        Assert.True(json.GetProperty("errors").EnumerateObject().Any());
        Assert.DoesNotContain("secret-supplied-value", json.GetRawText());
    }

    [Theory]
    [InlineData("/api/v1/examples/missing")]
    [InlineData("/api/v2/examples")]
    [InlineData("/swagger")]
    public async Task Unknown_resources_and_unimplemented_routes_return_problem_404(string path)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        await AssertProblem(await client.GetAsync(path), 404);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(409)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task Central_pipeline_preserves_failure_status_and_redacts_internals(int status)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Development").ConfigureServices(services =>
                services.AddControllers().AddApplicationPart(typeof(FailureFixtureController).Assembly)));
        using var client = factory.CreateClient();
        var response = await client.GetAsync($"/contract-fixture/{status}");
        var problem = await AssertProblem(response, status);
        Assert.DoesNotContain("secret-exception-value", problem.GetRawText());
        Assert.DoesNotContain("stack", problem.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("application/xml")]
    public async Task Status_and_exception_problems_ignore_unsupported_accept_headers(string accept)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(FailureFixtureController).Assembly)));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd(accept);
        await AssertProblem(await client.GetAsync("/missing"), 404);
        await AssertProblem(await client.GetAsync("/contract-fixture/500"), 500);
    }

    [Fact]
    public async Task Correlation_is_generated_per_request_and_does_not_trust_inbound_header()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "untrusted-value");
        var first = await client.GetAsync("/missing");
        var second = await client.GetAsync("/missing");
        var one = await AssertProblem(first, 404);
        var two = await AssertProblem(second, 404);
        Assert.NotEqual("untrusted-value", one.GetProperty("correlationId").GetString());
        Assert.NotEqual(one.GetProperty("correlationId").GetString(), two.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task Openapi_is_public_and_contains_only_production_operations()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var json = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = json.RootElement.GetProperty("paths");
        Assert.Equal(36, paths.EnumerateObject().Count());
        Assert.True(paths.TryGetProperty("/api/v1/examples", out _));
        Assert.False(paths.TryGetProperty("/contract-fixture/{status}", out _));
        Assert.True(json.RootElement.GetProperty("components").GetProperty("schemas").EnumerateObject().Any());
    }

    private static async Task<JsonElement> AssertProblem(HttpResponseMessage response, int status)
    {
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(status, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("instance").GetString()));
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), root.GetProperty("correlationId").GetString());
        return root.Clone();
    }
}

// Loaded explicitly by the test factory, never included in the production API assembly.
[ApiController]
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
[Route("contract-fixture/{status:int}")]
public sealed class FailureFixtureController : ControllerBase
{
    [HttpGet]
    public IActionResult Get(int status) => status == 500
        ? throw new InvalidOperationException("secret-exception-value") : StatusCode(status);
}
