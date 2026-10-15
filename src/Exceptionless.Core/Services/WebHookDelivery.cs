using System.Diagnostics;

namespace Exceptionless.Core.Services;

public static class WebHookDelivery
{
    public const string HttpClientName = "WebHooks";
    public static readonly HttpRequestOptionsKey<bool> DeliveryRequest = new("Exceptionless.WebHookDelivery");
    public static DistributedContextPropagator ContextPropagator { get; } = new WebHookContextPropagator();

    public static void EnrichActivity(Activity activity, HttpRequestMessage request)
    {
        if (request.Options.TryGetValue(DeliveryRequest, out bool delivery) && delivery)
            activity.SetTag("url.full", GetLoggingDestination(request.RequestUri?.AbsoluteUri));
    }

    public static string GetLoggingDestination(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (String.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) || String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            ? uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped)
            : "invalid";

    // External webhook receivers must not receive internal trace context or potentially sensitive baggage.
    // https://opentelemetry.io/docs/concepts/signals/baggage/#baggage-security-considerations
    private sealed class WebHookContextPropagator : DistributedContextPropagator
    {
        private readonly DistributedContextPropagator _noOutput = CreateNoOutputPropagator();

        public override IReadOnlyCollection<string> Fields => _noOutput.Fields;

        public override void Inject(Activity? activity, object? carrier, PropagatorSetterCallback? setter)
        {
            if (carrier is not HttpRequestMessage request)
                return;

            // OTel may already have injected headers before the runtime calls this propagator.
            // These are the runtime's default propagation fields. If B3, Jaeger, or another
            // OTel propagator is added, its headers must also be removed here and covered by tests.
            foreach (string field in Fields)
                request.Headers.Remove(field);
        }

        public override void ExtractTraceIdAndState(object? carrier, PropagatorGetterCallback? getter, out string? traceId, out string? traceState)
            => _noOutput.ExtractTraceIdAndState(carrier, getter, out traceId, out traceState);

        public override IEnumerable<KeyValuePair<string, string?>>? ExtractBaggage(object? carrier, PropagatorGetterCallback? getter)
            => _noOutput.ExtractBaggage(carrier, getter);
    }
}
