namespace FaultCode.Api.Configuration;

public sealed class QdrantOptions
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "http://qdrant:6333";
    public string? ApiKey { get; set; }
}
