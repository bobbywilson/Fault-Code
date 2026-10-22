using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FaultCode.Application.Review;
using FaultCode.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static FaultCode.IntegrationTests.EntraAuthenticationTests;
namespace FaultCode.IntegrationTests;

public sealed class TicketReviewTests
{
    private static readonly Guid Team = Guid.Parse("1af897bc-62dc-4e58-b385-cdccbc508849");
    private static readonly Guid Issue = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static object Summary(Guid? team = null) => new { id = Issue, identifier = "FAU-23", title = "Workflow", priorityLabel = "High", dueDate = (string?)null, createdAt = "2026-10-20T00:00:00Z", updatedAt = "2026-10-22T00:00:00Z", completedAt = (string?)null, url = "https://linear.app/fault-code/issue/FAU-23", state = new { name = "In Progress", type = "started" }, assignee = new { name = "Bobby" }, labels = new { nodes = new[] { new { name = "api" } } }, project = new { name = "Fault Code" }, team = new { id = team ?? Team } };
    private static object Scope(string workspace = "fault-code") => new { data = new { organization = new { urlKey = workspace }, teams = new { nodes = new[] { new { id = Team, key = "FAU" } }, pageInfo = new { hasNextPage = false } } } };
    private sealed class Handler(Func<JsonObject, object> reply) : HttpMessageHandler
    {
        public List<JsonObject> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("https://api.linear.app/graphql", request.RequestUri!.ToString());
            Assert.Equal("private-review-key", request.Headers.GetValues("Authorization").Single());
            var body = (await request.Content!.ReadFromJsonAsync<JsonObject>(ct))!;
            Assert.DoesNotContain("mutation", body["query"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
            Requests.Add(body); return new(HttpStatusCode.OK) { Content = JsonContent.Create(reply(body)) };
        }
    }
    [Fact]
    public async Task List_is_paginated_team_bound_cached_and_explicitly_refreshable()
    {
        using var handler = new Handler(body => body["query"]!.GetValue<string>().Contains("ReviewScope") ? Scope() :
            new { data = new { issues = new { nodes = new[] { Summary() }, pageInfo = new { hasNextPage = body["variables"]!["after"] is null, endCursor = "next" } } } });
        using var http = new HttpClient(handler); var source = new LinearTicketReview(http, "private-review-key", TimeProvider.System);
        var result = await source.List(false, default); Assert.Equal(2, result.Items.Length);
        Assert.All(result.Items, t => Assert.Equal("FAU-23", t.Identifier)); Assert.Equal(3, handler.Requests.Count);
        await source.List(false, default); Assert.Equal(3, handler.Requests.Count);
        await source.List(true, default); Assert.Equal(5, handler.Requests.Count);
        Assert.Equal(Team.ToString(), handler.Requests[1]["variables"]!["team"]!.GetValue<string>());
    }
    [Theory]
    [InlineData("workspace")]
    [InlineData("team")]
    [InlineData("partial")]
    public async Task Wrong_workspace_cross_team_and_GraphQL_partial_success_fail_closed(string failure)
    {
        using var handler = new Handler(body => body["query"]!.GetValue<string>().Contains("ReviewScope") ? Scope(failure == "workspace" ? "lalai" : "fault-code") :
            failure == "partial" ? new { data = new { }, errors = new[] { new { message = "private-provider-detail" } } } : (object)new { data = new { issues = new { nodes = new[] { Summary(Guid.NewGuid()) }, pageInfo = new { hasNextPage = false } } } });
        using var http = new HttpClient(handler); var source = new LinearTicketReview(http, "private-review-key", TimeProvider.System);
        var error = await Assert.ThrowsAsync<TicketReviewException>(() => source.List(false, default)); Assert.Equal(503, error.Status);
        Assert.DoesNotContain("private-provider-detail", error.ToString());
    }
    [Fact]
    public async Task Detail_retains_full_text_comments_and_review_metadata()
    {
        using var handler = new Handler(body =>
        {
            if (body["query"]!.GetValue<string>().Contains("ReviewScope")) return Scope();
            var item = JsonSerializerNode(Summary());
            item["description"] = "## Acceptance criteria\n- Verify workflow"; item["estimate"] = 3; item["creator"] = new JsonObject { ["name"] = "Bobby" }; item["cycle"] = null; item["parent"] = null;
            foreach (var field in new[] { "children", "relations", "inverseRelations", "attachments" }) item[field] = new JsonObject { ["nodes"] = new JsonArray(), ["pageInfo"] = new JsonObject { ["hasNextPage"] = false } };
            item["comments"] = new JsonObject { ["nodes"] = new JsonArray(new JsonObject { ["id"] = Guid.NewGuid().ToString(), ["body"] = "Important discussion", ["createdAt"] = "2026-10-20T00:00:00Z", ["updatedAt"] = "2026-10-21T00:00:00Z", ["user"] = new JsonObject { ["name"] = "Nathan" } }), ["pageInfo"] = new JsonObject { ["hasNextPage"] = false } };
            return new { data = new { issue = item } };
        });
        using var http = new HttpClient(handler); var detail = await new LinearTicketReview(http, "private-review-key", TimeProvider.System).Get(Issue, default);
        Assert.Contains("Acceptance criteria", detail.Description); Assert.Equal("Important discussion", Assert.Single(detail.Comments).Body); Assert.Equal(3, detail.Estimate);
    }
    private static JsonObject JsonSerializerNode(object value) => (JsonObject)System.Text.Json.JsonSerializer.SerializeToNode(value)!;
    private sealed class Source : ITicketReviewSource
    {
        public int Calls;
        public Task<ReviewTicketList> List(bool refresh, CancellationToken ct) { Calls++; return Task.FromResult(new ReviewTicketList([], DateTimeOffset.UtcNow, "Fault Code · FAU")); }
        public Task<ReviewTicketDetail> Get(Guid id, CancellationToken ct) => throw new TicketReviewException(404);
    }
    [Fact]
    public async Task Browser_review_accepts_demo_session_and_delegated_reader_but_not_anonymous_driver_or_machine()
    {
        using var keys = new Discovery(); var source = new Source();
        const string password = "isolated-session-test-password";
        await using var f = CreateFactory(keys).WithWebHostBuilder(b =>
        {
            b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<ITicketReviewSource>(source)));
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["DemoAccount:Enabled"] = "true", ["DemoAccount:DisplayName"] = "John Henry", ["DemoAccount:Username"] = "reviewer", ["DemoAccount:PasswordHash"] = new PasswordHasher<string>().HashPassword("reviewer", password), ["DemoAccount:UserId"] = Actor, ["DemoAccount:ClientId"] = Client, ["DemoAccount:TenantId"] = Tenant }));
        });
        using var client = f.CreateClient();
        using (var anonymous = await client.GetAsync("/workspace/tickets")) Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        foreach (var token in new[] { AuthorizationTests.Token(keys.Key, "faults.read", ["Driver"]), AuthorizationTests.Token(keys.Key, null, ["Faults.Read.All"], true) })
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", token); using var denied = await client.GetAsync("/workspace/tickets"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthorizationTests.Token(keys.Key, "faults.read", ["Reader"]));
        using (var reader = await client.GetAsync("/workspace/tickets")) { Assert.Equal(HttpStatusCode.OK, reader.StatusCode); Assert.Equal("no-store", reader.Headers.CacheControl!.ToString()); }
        client.DefaultRequestHeaders.Authorization = null;
        var session = (await client.GetFromJsonAsync<JsonObject>("/session"))!;
        using var login = new HttpRequestMessage(HttpMethod.Post, "/session/login") { Content = JsonContent.Create(new { username = "reviewer", password }) }; login.Headers.Add("X-FaultCode-CSRF", session["csrfToken"]!.GetValue<string>());
        using (var signed = await client.SendAsync(login)) Assert.Equal(HttpStatusCode.NoContent, signed.StatusCode);
        using (var demo = await client.GetAsync("/workspace/tickets")) Assert.Equal(HttpStatusCode.OK, demo.StatusCode);
        Assert.Equal(2, source.Calls);
    }
}
