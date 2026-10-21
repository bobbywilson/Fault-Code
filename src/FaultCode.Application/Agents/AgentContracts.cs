using System.Text.Json.Serialization;
using FaultCode.Application.Fleet;

namespace FaultCode.Application.Agents;

public sealed record AgentLimits(int TimeoutSeconds = 30, int MaxOutputTokens = 512, int TotalTokenBudget = 16000,
    int MaxRetries = 0, int MaxToolCalls = 0)
{
    public bool Valid => TimeoutSeconds is >= 1 and <= 120 && MaxOutputTokens is >= 64 and <= 2048
        && TotalTokenBudget is >= 1024 and <= 64000 && MaxRetries is >= 0 and <= 2 && MaxToolCalls == 0;
}
// Original authenticated caller retained in-process, never unioned with service permissions or sent as credentials.
public enum AgentPurpose { Advisory, Investigation }
public sealed record AgentInvocation(Guid RunId, Guid IncidentId, FleetCaller Caller, string Input, int MaxOutputTokens, AgentPurpose Purpose = AgentPurpose.Advisory);
public sealed record AgentCompletion(string Json, string Model, long? InputTokens, long? OutputTokens, string FinishReason, int ToolCalls);
public interface IAgentProvider
{
    bool Enabled { get; }
    string Model { get; }
    Task<AgentCompletion> Complete(AgentInvocation invocation, CancellationToken ct);
}
public sealed class AgentFailure(string code) : Exception
{
    public string Code { get; } = code;
    public int Status => Code switch
    {
        "agent-rate-limited" => 429,
        "agent-timeout" => 504,
        "agent-invalid-output" or "agent-refused" or "agent-provider-invalid" => 502,
        "agent-budget-exhausted" or "agent-tool-budget-exhausted" => 422,
        _ => 503
    };
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentEvidence(Guid DefinitionId, long PublicationVersion, string ClassificationId, long ClassificationVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentAdvice(Guid IncidentId, string Classification, AgentEvidence[] Evidence, string[] Uncertainties,
    string RecommendedAction, bool EscalationRequired);
public sealed record AgentRunResult(Guid RunId, Guid IncidentId, string CorrelationId, DateTimeOffset StartedAt,
    string Outcome, bool Advisory, AgentAdvice Advice, int Attempts, long InputTokens, long OutputTokens,
    int ReservedTokens, string Model, string InstructionsVersion);
