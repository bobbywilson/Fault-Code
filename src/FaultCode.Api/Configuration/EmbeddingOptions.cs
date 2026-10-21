namespace FaultCode.Api.Configuration;

public sealed class EmbeddingOptions
{
    public bool Enabled { get; set; }
    public string Model { get; set; } = "";
    public int Dimensions { get; set; }
    public string? ApiKey { get; set; }
}
