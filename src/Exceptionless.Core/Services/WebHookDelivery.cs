namespace Exceptionless.Core.Services;

public static class WebHookDelivery
{
    public const string HttpClientName = "WebHooks";
    public static readonly HttpRequestOptionsKey<bool> DeliveryRequest = new("Exceptionless.WebHookDelivery");

    public static string GetLoggingDestination(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped)
            : "invalid";
}
