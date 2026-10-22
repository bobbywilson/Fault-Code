namespace FaultCode.Api.Configuration;

public sealed class NotificationOptions
{
    public string Provider { get; set; } = "Mailpit";
    public string Endpoint { get; set; } = "http://127.0.0.1:8025/";
    public bool Valid => Provider == "Mailpit"
        && Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == "http" && (uri.IsLoopback || uri.Host == "mailpit") && uri.Port == 8025
        && uri.AbsolutePath == "/" && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;
}
