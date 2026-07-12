using Joonasw.AspNetCore.SecurityHeaders.Csp;
using Joonasw.AspNetCore.SecurityHeaders.Csp.Builder;

namespace Exceptionless.Web.Security;

internal static class FrontendContentSecurityPolicy
{
    public static void Configure(CspBuilder csp)
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
            .From("https://fonts.googleapis.com")
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
            .From("https://user-images.githubusercontent.com")
            .From("https://www.gravatar.com");

        csp.AllowFonts.FromSelf()
            .From("https://fonts.gstatic.com")
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
}
