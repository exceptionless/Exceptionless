using Joonasw.AspNetCore.SecurityHeaders.Csp;
using Joonasw.AspNetCore.SecurityHeaders.Csp.Builder;

namespace Exceptionless.Web.Security;

internal static class FrontendContentSecurityPolicy
{
    public static void Configure(CspBuilder csp)
    {
        Configure(csp, null);
    }

    public static void Configure(CspBuilder csp, string? siteBaseUrl)
    {
        // Exceptionless uses Intercom's US endpoints. Keep region-specific sources scoped to that workspace.
        csp.ByDefaultAllow.FromSelf();

        csp.AllowScripts.FromSelf()
            .AddNonce()
            .WithStrictDynamic()
            .From("https://*.js.stripe.com")
            .From("https://js.stripe.com")
            .From("https://maps.googleapis.com")
            .From("https://app.intercom.io")
            .From("https://widget.intercom.io")
            .From("https://js.intercomcdn.com");

        csp.AllowStyles.FromSelf()
            .AllowUnsafeInline()
            .From("https://cdn.jsdelivr.net");

        csp.AllowImages.FromSelf()
            .From("data:")
            .From("blob:")
            .From("https://*.stripe.com")
            .From("https://*.link.com")
            .From("https://js.intercomcdn.com")
            .From("https://static.intercomassets.com")
            .From("https://downloads.intercomcdn.com")
            .From("https://uploads.intercomcdn.com")
            .From("https://uploads.intercomusercontent.com")
            .From("https://gifs.intercomcdn.com")
            .From("https://video-messages.intercomcdn.com")
            .From("https://messenger-apps.intercom.io")
            .From("https://*.intercom-attachments-1.com")
            .From("https://*.intercom-attachments-2.com")
            .From("https://*.intercom-attachments-3.com")
            .From("https://*.intercom-attachments-4.com")
            .From("https://*.intercom-attachments-5.com")
            .From("https://*.intercom-attachments-6.com")
            .From("https://*.intercom-attachments-7.com")
            .From("https://*.intercom-attachments-8.com")
            .From("https://*.intercom-attachments-9.com")
            .From("https://www.gravatar.com");

        csp.AllowFonts.FromSelf()
            .From("https://js.intercomcdn.com")
            .From("https://fonts.intercomcdn.com")
            .From("https://cdn.jsdelivr.net");

        csp.AllowConnections.ToSelf()
            .To("https://collector.exceptionless.io")
            .To("https://config.exceptionless.io")
            .To("https://heartbeat.exceptionless.io")
            .To("https://api.stripe.com")
            .To("https://maps.googleapis.com")
            .To("https://link.com")
            .To("https://*.link.com")
            .To("https://via.intercom.io")
            .To("https://api.intercom.io")
            .To("https://api-iam.intercom.io")
            .To("https://api-ping.intercom.io")
            .To("https://*.intercom-messenger.com")
            .To("wss://*.intercom-messenger.com")
            .To("https://nexus-websocket-a.intercom.io")
            .To("wss://nexus-websocket-a.intercom.io")
            .To("https://nexus-websocket-b.intercom.io")
            .To("wss://nexus-websocket-b.intercom.io")
            .To("https://uploads.intercomcdn.com")
            .To("https://uploads.intercomusercontent.com");

        // Use administrator configuration, never request Host or forwarded headers.
        // Some browsers do not match WebSocket schemes against connect-src 'self'.
        if (siteBaseUrl is not null)
            csp.AllowConnections.To(GetWebSocketOrigin(siteBaseUrl));

        csp.AllowFrames.FromSelf()
            .From("https://*.js.stripe.com")
            .From("https://js.stripe.com")
            .From("https://hooks.stripe.com")
            .From("https://link.com")
            .From("https://*.link.com")
            .From("https://intercom-sheets.com")
            .From("https://www.intercom-reporting.com")
            .From("https://www.youtube.com")
            .From("https://player.vimeo.com")
            .From("https://fast.wistia.net");

        csp.AllowAudioAndVideo.FromSelf()
            .From("blob:")
            .From("https://js.intercomcdn.com")
            .From("https://downloads.intercomcdn.com");

        csp.AllowWorkers.FromSelf()
            .From("blob:")
            .From("https://intercom-sheets.com")
            .From("https://www.intercom-reporting.com")
            .From("https://www.youtube.com")
            .From("https://player.vimeo.com")
            .From("https://fast.wistia.net");

        csp.AllowFormActions.ToSelf()
            .To("https://intercom.help")
            .To("https://api-iam.intercom.io");
        csp.AllowManifest.FromSelf();
        csp.AllowPlugins.FromNowhere();
        csp.AllowBaseUri.FromNowhere();
        csp.AllowFraming.FromNowhere();

        csp.OnSendingHeader = context =>
        {
            context.ShouldNotSend = context.HttpContext.Request.Path.StartsWithSegments("/api");
            return Task.CompletedTask;
        };
    }

    internal static string GetWebSocketOrigin(string siteBaseUrl)
    {
        if (!Uri.TryCreate(siteBaseUrl, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !String.IsNullOrEmpty(uri.UserInfo)
            || uri.HostNameType is not (UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6)
            || uri.Host.Contains('*'))
            throw new ArgumentException("The CSP site base URL must be an absolute HTTP(S) URL without credentials or wildcard hosts.", nameof(siteBaseUrl));

        var origin = new UriBuilder(uri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws", uri.Host, uri.IsDefaultPort ? -1 : uri.Port);
        return origin.Uri.GetLeftPart(UriPartial.Authority);
    }
}
