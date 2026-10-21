namespace FaultCode.Infrastructure.Telemetry;

public sealed record OtlpTelemetrySettings(string Endpoint, double TraceSampleRatio = 0.1, string? Model = null)
{
    public static bool HasConflictingEnvironment => Environment.GetEnvironmentVariables().Keys.Cast<string>().Any(k => k.StartsWith("OTEL_", StringComparison.OrdinalIgnoreCase));
    public bool Valid => !HasConflictingEnvironment && double.IsFinite(TraceSampleRatio) && TraceSampleRatio is >= 0 and <= 1
        && Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
        && (uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback)
        && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 && uri.AbsolutePath == "/"
        && (Model is null || Model.Length is >= 12 and <= 100 && Model.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')
            && System.Text.RegularExpressions.Regex.IsMatch(Model, @"-\d{4}-\d{2}-\d{2}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant));
}
