namespace FaultCode.Api.Configuration;

public sealed class AgentOptions
{
    public bool Enabled { get; set; }
    public string Model { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxOutputTokens { get; set; } = 512;
    public int TotalTokenBudget { get; set; } = 16000;
    public int MaxRetries { get; set; }
    public int MaxToolCalls { get; set; }
    public FaultCode.Application.Agents.AgentLimits Limits => new(TimeoutSeconds, MaxOutputTokens, TotalTokenBudget, MaxRetries, MaxToolCalls);
    public bool Valid => Limits.Valid && (!Enabled || (!string.IsNullOrWhiteSpace(ApiKey) && !ApiKey.Any(char.IsControl)
        && Model.Length is >= 12 and <= 100 && Model.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')
        && System.Text.RegularExpressions.Regex.IsMatch(Model, @"-\d{4}-\d{2}-\d{2}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)));
}
