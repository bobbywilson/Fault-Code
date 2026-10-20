namespace FaultCode.Api.Configuration;

public sealed class EntraOptions
{
    public bool Enabled { get; set; }
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";

    public static bool IsIdentifier(string value) => Guid.TryParseExact(value, "D", out var id)
        && id != Guid.Empty && value == id.ToString("D");
}
