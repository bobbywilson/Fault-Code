using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FaultCode.Application.Agents;
using FaultCode.Application.Fleet;
using FaultCode.Infrastructure;

namespace FaultCode.IntegrationTests;

public sealed class AgentProviderTests
{
    private const string Model = "demo-agent-2026-10-20";
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => action(request, ct); }
    private static readonly Guid Id = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static AgentInvocation Input => new(Guid.NewGuid(), Id,
        new("11111111-1111-4111-8111-111111111111", "22222222-2222-4222-8222-222222222222", "33333333-3333-4333-8333-333333333333", "demo", true, false, true, false),
        "{\"incidentId\":\"55555555-5555-4555-8555-555555555555\",\"code\":\"SYN-EPS:001\"}", 512);
    private static object Response(string finish = "stop", string? refusal = null, object[]? tools = null) => new
    {
        id = "chatcmpl-demo",
        @object = "chat.completion",
        created = 1792530000,
        model = Model,
        choices = new[] { new { index = 0, message = new { role = "assistant", content = "{}", refusal, tool_calls = tools }, finish_reason = finish } },
        usage = new { prompt_tokens = 300, completion_tokens = 30, total_tokens = 330 }
    };
    [Fact]
    public async Task Real_framework_and_OpenAI_sdk_emit_strict_schema_no_tools_and_bounded_output()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(async (request, ct) =>
        {
            calls++; Assert.Equal("https://api.openai.com/v1/chat/completions", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)); var b = json.RootElement;
            Assert.Equal(Model, b.GetProperty("model").GetString());
            Assert.True(b.TryGetProperty("max_completion_tokens", out var max) || b.TryGetProperty("max_tokens", out max)); Assert.Equal(512, max.GetInt32());
            var format = b.GetProperty("response_format"); Assert.Equal("json_schema", format.GetProperty("type").GetString());
            Assert.True(format.GetProperty("json_schema").GetProperty("strict").GetBoolean());
            Assert.False(format.GetProperty("json_schema").GetProperty("schema").GetProperty("additionalProperties").GetBoolean());
            Assert.False(b.TryGetProperty("tools", out _));
            var messages = b.GetProperty("messages"); Assert.Equal(2, messages.GetArrayLength());
            Assert.Contains("UNTRUSTED", messages[0].GetProperty("content").GetString());
            Assert.DoesNotContain(Input.Caller.ActorId, b.ToString());
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(Response()) };
        }));
        var p = new AgentFrameworkProvider(http, true, Model, "demo-not-live-key");
        var result = await p.Complete(Input, default); Assert.Equal(Model, result.Model); Assert.Equal(300, result.InputTokens);
        Assert.Equal(30, result.OutputTokens); Assert.Equal("stop", result.FinishReason); Assert.Equal(0, result.ToolCalls);
        await p.Complete(Input, default); Assert.Equal(2, calls);
    }
    [Theory]
    [InlineData(429, "agent-rate-limited")]
    [InlineData(503, "agent-provider-unavailable")]
    [InlineData(408, "agent-provider-unavailable")]
    [InlineData(401, "agent-provider-rejected")]
    [InlineData(400, "agent-provider-rejected")]
    [InlineData(302, "agent-provider-rejected")]
    public async Task Sdk_has_no_hidden_retry_and_provider_bodies_do_not_escape(int status, string expected)
    {
        var calls = 0;
        using var http = new HttpClient(new Handler((_, _) =>
        {
            calls++; return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
            { Content = new StringContent("secret-provider-body demo-not-live-key") });
        }));
        var error = await Assert.ThrowsAsync<AgentFailure>(() => new AgentFrameworkProvider(http, true, Model, "demo-not-live-key").Complete(Input, default));
        Assert.Equal(expected, error.Code); Assert.DoesNotContain("secret-provider-body", error.ToString()); Assert.Equal(1, calls);
    }
    [Fact]
    public async Task Actual_sdk_surfaces_refusal_length_and_tool_calls_without_executing_them()
    {
        foreach (var mode in new[] { "refused", "length", "tool_calls" })
        {
            var calls = 0;
            using var http = new HttpClient(new Handler((_, _) =>
            {
                calls++;
                object[]? tools = mode == "tool_calls" ? [new { id = "call-demo", type = "function", function = new { name = "publishKnowledge", arguments = "{}" } }] : null;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Response(mode == "refused" ? "stop" : mode, mode == "refused" ? "secret-provider-body" : null, tools)) });
            }));
            var response = await new AgentFrameworkProvider(http, true, Model, "demo-not-live-key").Complete(Input, default);
            if (mode == "tool_calls") Assert.Equal(1, response.ToolCalls); else Assert.Equal(mode, response.FinishReason);
            Assert.Equal(1, calls);
        }
    }
    [Fact]
    public async Task Actual_sdk_cancellation_and_transport_failure_are_safe()
    {
        using var http = new HttpClient(new Handler(async (_, ct) => { await Task.Delay(Timeout.InfiniteTimeSpan, ct); return new(HttpStatusCode.OK); }));
        using var cancel = new CancellationTokenSource(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AgentFrameworkProvider(http, true, Model, "demo-not-live-key").Complete(Input, cancel.Token));
        using var down = new HttpClient(new Handler((_, _) => throw new HttpRequestException("secret-provider-body")));
        Assert.Equal("agent-provider-unavailable", (await Assert.ThrowsAsync<AgentFailure>(() => new AgentFrameworkProvider(down, true, Model, "demo-not-live-key").Complete(Input, default))).Code);
    }
    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"choices\":[]}")]
    public async Task Malformed_provider_envelopes_have_safe_typed_failure(string raw)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(raw) })));
        Assert.Equal("agent-provider-invalid", (await Assert.ThrowsAsync<AgentFailure>(() => new AgentFrameworkProvider(http, true, Model, "demo-not-live-key").Complete(Input, default))).Code);
    }

    [Fact]
    public async Task Provider_response_buffer_is_bounded_before_sdk_deserialization()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(new string('x', 65537)) })));
        using var provider = new AgentFrameworkProvider(http, true, Model, "demo-not-live-key");
        Assert.Equal("agent-provider-unavailable", (await Assert.ThrowsAsync<AgentFailure>(() => provider.Complete(Input, default))).Code);
    }

}
