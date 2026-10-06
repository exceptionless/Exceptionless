using System.Text.RegularExpressions;
using Joonasw.AspNetCore.SecurityHeaders.Csp.Builder;
using Microsoft.Extensions.FileProviders;

namespace Exceptionless.Web.Security;

internal static partial class FrontendContentSecurityPolicy
{
    public static void Configure(CspBuilder csp, IFileProvider files, bool upgradeInsecureRequests, string? siteBaseUrl = null, string? apiUrl = null)
    {
        // Same-origin resources are the fallback for directives without their own source list.
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/default-src
        csp.ByDefaultAllow.FromSelf();

        // Nonces/hashes authorize our bootstrap; strict-dynamic trusts scripts it loads.
        // Provider host wildcards consolidate Stripe.js and Intercom's script host families.
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/script-src
        // https://docs.stripe.com/security/guide#content-security-policy
        // https://www.intercom.com/help/en/articles/3894-using-intercom-with-content-security-policy
        csp.AllowScripts.FromSelf().AddNonce().WithStrictDynamic()
            .From("https://*.stripe.com")
            .From("https://*.intercom.io")
            .From("https://js.intercomcdn.com");

        // Trust only hashes from the published SPA, never request or response HTML.
        // SvelteKit generates these for its bootstrap; static responses need no nonce rewriting.
        // https://svelte.dev/docs/kit/configuration#csp
        IFileInfo index = files.GetFileInfo("index.html");
        if (index.Exists)
        {
            using var reader = new StreamReader(index.CreateReadStream());
            foreach (Match hash in ScriptHashRegex().Matches(reader.ReadToEnd()))
                csp.AllowScripts.From(hash.Value);
        }

        // UI style attributes and Scalar/Intercom's injected styles still need inline CSS.
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/style-src
        // https://www.intercom.com/help/en/articles/3894-using-intercom-with-content-security-policy
        csp.AllowStyles.FromSelf().AllowUnsafeInline();

        // Local previews, Stripe Link assets, core Intercom assets and user Gravatar images.
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/img-src
        // https://docs.stripe.com/security/guide#content-security-policy
        // https://www.intercom.com/help/en/articles/3894-using-intercom-with-content-security-policy
        // https://docs.gravatar.com/sdk/images/
        csp.AllowImages.FromSelf().From("blob:").From("data:")
            .From("https://*.link.com")
            .From("https://js.intercomcdn.com")
            .From("https://static.intercomassets.com")
            .From("https://www.gravatar.com");
        // Bundled app fonts and Intercom's js/fonts CDN hosts.
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/font-src
        // https://www.intercom.com/help/en/articles/3894-using-intercom-with-content-security-policy
        csp.AllowFonts.FromSelf().From("https://*.intercomcdn.com");

        // The backend serves a fixed policy; only Vite allows arbitrary environment connections.
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/connect-src
        csp.AllowConnections.ToSelf()
            // Browser telemetry to Exceptionless collectors (hooks.client.ts).
            // https://exceptionless.com/docs/clients/javascript/
            .To("https://*.exceptionless.io")
            // Payment Element uses Stripe.js; Link is enabled by its default payment options.
            // https://docs.stripe.com/security/guide#content-security-policy
            .To("https://api.stripe.com")
            .To("https://link.com").To("https://*.link.com")
            // Messenger API/ping and realtime connections; no upload or attachment hosts.
            // https://www.intercom.com/help/en/articles/3894-using-intercom-with-content-security-policy
            .To("https://*.intercom.io").To("wss://*.intercom.io")
            .To("https://*.intercom-messenger.com").To("wss://*.intercom-messenger.com");
        // Explicit configured WebSocket origins cover browsers where 'self' does not match WSS.
        // BaseURL/ApiUrl must match the externally served origins, including behind reverse proxies.
        // Invalid or missing origins add no sources; request/forwarded headers are never trusted.
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/connect-src
        ApiContentSecurityPolicy.AllowConfiguredOrigins(csp, siteBaseUrl);
        ApiContentSecurityPolicy.AllowConfiguredOrigins(csp, apiUrl);

        // Stripe Payment Element, 3DS and Link frames; self includes the Scalar request editor.
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/frame-src
        // https://docs.stripe.com/security/guide#content-security-policy
        csp.AllowFrames.FromSelf().From("https://*.stripe.com")
            .From("https://link.com").From("https://*.link.com");

        // Intercom's core messenger sounds; optional video/attachment sources are excluded.
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/media-src
        // https://www.intercom.com/help/en/articles/3894-using-intercom-with-content-security-policy
        csp.AllowAudioAndVideo.FromSelf().From("https://js.intercomcdn.com");

        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/worker-src
        csp.AllowWorkers.FromSelf();
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/form-action
        csp.AllowFormActions.ToSelf();
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/manifest-src
        csp.AllowManifest.FromSelf();
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/object-src
        csp.AllowPlugins.FromNowhere();
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/base-uri
        csp.AllowBaseUri.FromNowhere();
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/frame-ancestors
        csp.AllowFraming.FromNowhere();
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/upgrade-insecure-requests
        if (upgradeInsecureRequests)
            csp.SetUpgradeInsecureRequests();

        csp.OnSendingHeader = context =>
        {
            context.ShouldNotSend = context.HttpContext.Request.Path.StartsWithSegments("/api");
            return Task.CompletedTask;
        };
    }

    [GeneratedRegex("'sha256-[A-Za-z0-9+/]{43}='", RegexOptions.CultureInvariant)]
    private static partial Regex ScriptHashRegex();
}
