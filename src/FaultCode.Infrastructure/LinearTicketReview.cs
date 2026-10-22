using System.Net.Http.Json;
using System.Text.Json;
using FaultCode.Application.Review;

namespace FaultCode.Infrastructure;

// One fixed workspace/team. Neither the browser nor ticket text can choose an
// upstream query, URL, organization or credential. No mutations are implemented.
public sealed class LinearTicketReview(HttpClient http, string key, TimeProvider clock) : ITicketReviewSource
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private ReviewTicketList? cached;
    private string? team;
    private const string SummaryFields = "id identifier title priorityLabel dueDate createdAt updatedAt completedAt url state { name type } assignee { name } labels(first:100) { nodes { name } } project { name }";
    private const string ReferenceFields = "id identifier title state { name } team { id }";
    private const string CommentFields = "id body createdAt updatedAt user { name }";
    private static string? Text(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string Required(JsonElement item, string name) => Text(item, name) ?? throw new TicketReviewException(503);
    private static DateTimeOffset At(JsonElement item, string name) => DateTimeOffset.Parse(Required(item, name), System.Globalization.CultureInfo.InvariantCulture);
    private static string? Named(JsonElement item, string name) => item.TryGetProperty(name, out var nested) && nested.ValueKind == JsonValueKind.Object ? Text(nested, "name") : null;
    private static string Link(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo) ? value! : "";
    private async Task<JsonElement> Query(string query, object variables, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 256 || key.Any(char.IsWhiteSpace) || key.Any(char.IsControl)) throw new TicketReviewException(503);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.linear.app/graphql")
        {
            Content = JsonContent.Create(new { query, variables })
        };
        request.Headers.Add("Authorization", key);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var response = await http.SendAsync(request, deadline.Token);
            if (!response.IsSuccessStatusCode) throw new TicketReviewException(503);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(deadline.Token));
            var root = json.RootElement;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind != JsonValueKind.Null && errors.GetArrayLength() > 0) throw new TicketReviewException(503);
            return root.GetProperty("data").Clone();
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException
            || e is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new TicketReviewException(503); }
    }
    private async Task<Guid> Team(CancellationToken ct)
    {
        if (team is { } known) return Guid.Parse(known);
        var data = await Query("query ReviewScope { organization { urlKey } teams(first:100) { nodes { id key } pageInfo { hasNextPage } } }", new { }, ct);
        if (Required(data.GetProperty("organization"), "urlKey") != "fault-code") throw new TicketReviewException(503);
        var teams = data.GetProperty("teams");
        if (teams.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean()) throw new TicketReviewException(503);
        var matches = teams.GetProperty("nodes").EnumerateArray().Where(t => Text(t, "key") == "FAU").ToArray();
        if (matches.Length != 1 || !Guid.TryParse(Text(matches[0], "id"), out var id)) throw new TicketReviewException(503);
        team = id.ToString("D"); return id;
    }
    private static ReviewTicket Summary(JsonElement issue) => new(Guid.Parse(Required(issue, "id")), Required(issue, "identifier"),
        Required(issue, "title"), Required(issue.GetProperty("state"), "name"), Required(issue.GetProperty("state"), "type"),
        Required(issue, "priorityLabel"), Named(issue, "assignee"), issue.GetProperty("labels").GetProperty("nodes").EnumerateArray().Select(l => Required(l, "name")).ToArray(),
        Named(issue, "project"), At(issue, "createdAt"), At(issue, "updatedAt"), Text(issue, "dueDate"), Text(issue, "completedAt") is { } completed ? DateTimeOffset.Parse(completed, System.Globalization.CultureInfo.InvariantCulture) : null, Link(Text(issue, "url")));
    public async Task<ReviewTicketList> List(bool refresh, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (!refresh && cached is { } prior && prior.RetrievedAt.AddMinutes(2) > clock.GetUtcNow()) return prior;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(45));
            var teamId = await Team(deadline.Token); var items = new List<ReviewTicket>(); string? cursor = null; var pages = 0;
            do
            {
                var data = await Query("query ReviewTickets($team:ID!,$after:String) { issues(first:100,after:$after,filter:{team:{id:{eq:$team}}},includeArchived:false) { nodes { " + SummaryFields + " team { id } } pageInfo { hasNextPage endCursor } } }", new { team = teamId, after = cursor }, deadline.Token);
                var connection = data.GetProperty("issues");
                foreach (var item in connection.GetProperty("nodes").EnumerateArray())
                {
                    if (Guid.Parse(Required(item.GetProperty("team"), "id")) != teamId) throw new TicketReviewException(503);
                    items.Add(Summary(item));
                }
                cursor = Next(connection); if (++pages >= 5 && cursor is not null) throw new TicketReviewException(503);
            } while (cursor is not null);
            cached = new(items.OrderBy(t => int.TryParse(t.Identifier.Split('-').Last(), out var number) ? number : int.MaxValue).ToArray(), clock.GetUtcNow(), "Fault Code · FAU");
            return cached;
        }
        catch (Exception e) when (e is FormatException or KeyNotFoundException or InvalidOperationException || e is OperationCanceledException && !ct.IsCancellationRequested) { throw new TicketReviewException(503); }
        finally { gate.Release(); }
    }
    private static string? Next(JsonElement connection) => connection.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean()
        ? Text(connection.GetProperty("pageInfo"), "endCursor") ?? throw new TicketReviewException(503) : null;
    private static ReviewTicketReference? Reference(JsonElement issue, Guid team, string relationship)
    {
        if (issue.ValueKind != JsonValueKind.Object || Guid.Parse(Required(issue.GetProperty("team"), "id")) != team) return null;
        return new(Guid.Parse(Required(issue, "id")), Required(issue, "identifier"), Required(issue, "title"), Required(issue.GetProperty("state"), "name"), relationship);
    }
    public async Task<ReviewTicketDetail> Get(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) throw new TicketReviewException(404);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(45));
        var requestToken = deadline.Token;
        try
        {
            var teamId = await Team(requestToken);
            var data = await Query("query ReviewTicket($id:String!) { issue(id:$id) { " + SummaryFields + " description estimate creator { name } cycle { name number } projectMilestone { name } team { id } parent { " + ReferenceFields + " } children(first:100) { nodes { " + ReferenceFields + " } pageInfo { hasNextPage endCursor } } relations(first:100) { nodes { type relatedIssue { " + ReferenceFields + " } } pageInfo { hasNextPage } } inverseRelations(first:100) { nodes { type issue { " + ReferenceFields + " } } pageInfo { hasNextPage } } attachments(first:100) { nodes { title subtitle url } pageInfo { hasNextPage } } comments(first:100) { nodes { " + CommentFields + " } pageInfo { hasNextPage endCursor } } } }", new { id = id.ToString("D") }, requestToken);
            var issue = data.GetProperty("issue");
            if (issue.ValueKind == JsonValueKind.Null || Guid.Parse(Required(issue.GetProperty("team"), "id")) != teamId) throw new TicketReviewException(404);
            var comments = new List<ReviewTicketComment>(); var commentConnection = issue.GetProperty("comments"); var commentPages = 0;
            while (true)
            {
                comments.AddRange(commentConnection.GetProperty("nodes").EnumerateArray().Select(c => new ReviewTicketComment(Guid.Parse(Required(c, "id")), Named(c, "user"), Required(c, "body"), At(c, "createdAt"), At(c, "updatedAt"))));
                var cursor = Next(commentConnection); if (cursor is null) break;
                if (++commentPages >= 5) throw new TicketReviewException(503);
                var more = (await Query("query ReviewComments($id:String!,$after:String) { issue(id:$id) { team { id } comments(first:100,after:$after) { nodes { " + CommentFields + " } pageInfo { hasNextPage endCursor } } } }", new { id = id.ToString("D"), after = cursor }, requestToken)).GetProperty("issue");
                if (Guid.Parse(Required(more.GetProperty("team"), "id")) != teamId) throw new TicketReviewException(404);
                commentConnection = more.GetProperty("comments");
            }
            var subtasks = new List<ReviewTicketReference>(); var childConnection = issue.GetProperty("children"); var childPages = 0;
            while (true)
            {
                subtasks.AddRange(childConnection.GetProperty("nodes").EnumerateArray().Select(c => Reference(c, teamId, "Subtask")).OfType<ReviewTicketReference>());
                var cursor = Next(childConnection); if (cursor is null) break;
                if (++childPages >= 5) throw new TicketReviewException(503);
                var more = (await Query("query ReviewChildren($id:String!,$after:String) { issue(id:$id) { team { id } children(first:100,after:$after) { nodes { " + ReferenceFields + " } pageInfo { hasNextPage endCursor } } } }", new { id = id.ToString("D"), after = cursor }, requestToken)).GetProperty("issue");
                if (Guid.Parse(Required(more.GetProperty("team"), "id")) != teamId) throw new TicketReviewException(404);
                childConnection = more.GetProperty("children");
            }
            foreach (var field in new[] { "relations", "inverseRelations", "attachments" })
                if (issue.GetProperty(field).GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean()) throw new TicketReviewException(503);
            var related = issue.GetProperty("relations").GetProperty("nodes").EnumerateArray().Select(r => Reference(r.GetProperty("relatedIssue"), teamId, Required(r, "type")))
                .Concat(issue.GetProperty("inverseRelations").GetProperty("nodes").EnumerateArray().Select(r => Reference(r.GetProperty("issue"), teamId, "Incoming " + Required(r, "type")))).OfType<ReviewTicketReference>().ToArray();
            var cycle = Named(issue, "cycle"); if (cycle is null && issue.TryGetProperty("cycle", out var cycleData) && cycleData.ValueKind == JsonValueKind.Object) cycle = "Cycle " + cycleData.GetProperty("number").GetInt32();
            var attachments = issue.GetProperty("attachments").GetProperty("nodes").EnumerateArray().Select(a => new ReviewTicketAttachment(Required(a, "title"), Text(a, "subtitle"), Link(Text(a, "url")))).Where(a => a.Url.Length > 0).ToArray();
            return new(Summary(issue), Text(issue, "description"), Named(issue, "creator"), cycle, Named(issue, "projectMilestone"), issue.TryGetProperty("estimate", out var estimate) && estimate.ValueKind == JsonValueKind.Number ? estimate.GetDecimal() : null,
                Reference(issue.GetProperty("parent"), teamId, "Parent"), subtasks.ToArray(), related, comments.OrderBy(c => c.CreatedAt).ToArray(), attachments, clock.GetUtcNow());
        }
        catch (Exception e) when (e is FormatException or KeyNotFoundException or InvalidOperationException || e is OperationCanceledException && !ct.IsCancellationRequested) { throw new TicketReviewException(503); }
    }
}
