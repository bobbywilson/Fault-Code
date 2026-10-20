namespace FaultCode.Api.Configuration;

public sealed class FoundationOptions
{
    public const string SectionName = "FaultCode";

    public string ApplicationName { get; set; } = string.Empty;
}
