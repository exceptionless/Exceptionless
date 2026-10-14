using System.Diagnostics;

namespace Exceptionless.Core.Services;

public static class WebHookDelivery
{
    public const string HttpClientName = "WebHooks";
    public static readonly HttpRequestOptionsKey<bool> DeliveryRequest = new("Exceptionless.WebHookDelivery");

    public static void EnrichActivity(Activity activity, HttpRequestMessage request)
    {
        if (request.Options.TryGetValue(DeliveryRequest, out bool delivery) && delivery)
            activity.SetTag("url.full", GetLoggingDestination(request.RequestUri?.AbsoluteUri));
    }

    public static string GetLoggingDestination(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped)
            : "invalid";
}
