namespace FaultCode.Api.Configuration;

public sealed class DatadogIncidentOptions
{
    public string AccessToken { get; set; } = "";
    public bool Valid => AccessToken.Length <= 1024 && !AccessToken.Any(char.IsControl);
}
