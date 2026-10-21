using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using FaultCode.Application.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace FaultCode.Infrastructure;

public sealed class AgentFrameworkProvider : IAgentProvider, IDisposable
{
    public bool Enabled { get; }
    public string Model { get; }
    private readonly IChatClient? client;
    private readonly HttpClient boundedHttp;
    public AgentFrameworkProvider(HttpClient http, bool enabled, string model, string apiKey)
    {
        Enabled = enabled; Model = model;
        boundedHttp = new HttpClient(new BoundedResponseHandler(http)) { Timeout = Timeout.InfiniteTimeSpan };
        if (enabled)
        {
            var options = new OpenAIClientOptions
            {
                Endpoint = new Uri("https://api.openai.com/v1"),
                Transport = new HttpClientPipelineTransport(boundedHttp),
                RetryPolicy = new ClientRetryPolicy(0),
                ClientLoggingOptions = new ClientLoggingOptions { EnableLogging = false, EnableMessageContentLogging = false }
            };
            client = new OpenAIClient(new ApiKeyCredential(apiKey), options).GetChatClient(model).AsIChatClient();
        }
    }
    public const string Schema = """
        {"type":"object","additionalProperties":false,"properties":{
        "incidentId":{"type":"string"},"classification":{"type":"string"},
        "evidence":{"type":"array","items":{"type":"object","additionalProperties":false,"properties":{
        "definitionId":{"type":"string"},"publicationVersion":{"type":"integer"},"classificationId":{"type":"string"},"classificationVersion":{"type":"integer"}},
        "required":["definitionId","publicationVersion","classificationId","classificationVersion"]}},
        "uncertainties":{"type":"array","items":{"type":"string","enum":["missing-evidence","unadjudicated-evidence","investigation-pending"]}},
        "recommendedAction":{"type":"string","enum":["engineering-review"]},"escalationRequired":{"type":"boolean","enum":[true]}},
        "required":["incidentId","classification","evidence","uncertainties","recommendedAction","escalationRequired"]}
        """;
    public async Task<AgentCompletion> Complete(AgentInvocation invocation, CancellationToken ct)
    {
        if (client is null) throw new AgentFailure("agent-not-configured");
        try
        {
            // A fresh agent/session prevents cross-run or cross-tenant conversation reuse. No tools registered.
            var agent = new ChatClientAgent(client, new ChatClientAgentOptions
            {
                Name = "FaultCodeAdvisory",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = AgentRuntimeService.Instructions,
                    MaxOutputTokens = invocation.MaxOutputTokens,
                    RawRepresentationFactory = _ => new OpenAI.Chat.ChatCompletionOptions
                    { ResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat("fault_code_advice", BinaryData.FromString(Schema), jsonSchemaIsStrict: true) }
                }
            });
            var response = await agent.RunAsync(invocation.Input, cancellationToken: ct);
            var content = response.Messages.SelectMany(m => m.Contents).ToArray();
            var tools = content.OfType<FunctionCallContent>().Count();
            return new(response.Text, response.AsOpenAIChatCompletion().Model, response.Usage?.InputTokenCount, response.Usage?.OutputTokenCount,
                !string.IsNullOrEmpty(response.AsOpenAIChatCompletion().Refusal) || content.OfType<ErrorContent>().Any() ? "refused" : response.FinishReason?.ToString() ?? "", tools);
        }
        catch (ClientResultException e)
        {
            throw new AgentFailure(e.Status == 429 ? "agent-rate-limited" : e.Status is >= 500 or 408 or 0 ? "agent-provider-unavailable" : "agent-provider-rejected");
        }
        catch (HttpRequestException) { throw new AgentFailure("agent-provider-unavailable"); }
        catch (JsonException) { throw new AgentFailure("agent-provider-invalid"); }
        catch (InvalidOperationException) { throw new AgentFailure("agent-provider-invalid"); }
        catch (ArgumentException) { throw new AgentFailure("agent-provider-invalid"); }
    }
    public void Dispose() { client?.Dispose(); boundedHttp.Dispose(); }
    private sealed class BoundedResponseHandler(HttpClient upstream) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var forwarded = new HttpRequestMessage(request.Method, request.RequestUri)
            { Content = request.Content, Version = request.Version, VersionPolicy = request.VersionPolicy };
            foreach (var header in request.Headers) forwarded.Headers.TryAddWithoutValidation(header.Key, header.Value);
            var response = await upstream.SendAsync(forwarded, HttpCompletionOption.ResponseHeadersRead, ct);
            try { await response.Content.LoadIntoBufferAsync(65536, ct); return response; }
            catch { response.Dispose(); throw; }
        }
    }

}
