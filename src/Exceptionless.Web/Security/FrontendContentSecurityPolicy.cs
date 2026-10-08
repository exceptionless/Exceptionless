using System.Text.RegularExpressions;
using Joonasw.AspNetCore.SecurityHeaders.Csp.Builder;
using Microsoft.Extensions.FileProviders;

namespace Exceptionless.Web.Security;

internal static partial class FrontendContentSecurityPolicy
{
    // CSP directives and source syntax:
    // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy
    // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/default-src
    // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/script-src
    // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/connect-src
    // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/style-src
    // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/img-src
    // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/font-src
    // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/frame-src
    // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/media-src
    // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/worker-src
    // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/form-action
    // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/object-src
    // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/base-uri
    // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/frame-ancestors
    // https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/upgrade-insecure-requests
    // Build-generated hashes and provider requirements:
    // https://svelte.dev/docs/kit/configuration#csp
    // https://docs.stripe.com/security/guide#content-security-policy
    // https://www.intercom.com/help/en/articles/3894-using-intercom-with-content-security-policy
    // https://scalar.com/products/api-references/integrations/aspnetcore/integration#assets
    // https://scalar.com/products/api-references/configuration#agent
    // https://docs.gravatar.com/sdk/images/
    // https://exceptionless.com/docs/clients/javascript/
    public static void Configure(CspBuilder csp, IFileProvider files, bool upgradeInsecureRequests, string? siteBaseUrl = null, string? apiUrl = null)
    {
        // This header is the published SPA's security boundary. The static build's meta policy
        // permits runtime/Vite connection targets and cannot enforce frame-ancestors.
        // Deny resource types unless their directive explicitly allows them.
        csp.ByDefaultAllow.FromNowhere();

        // Nonces/hashes authorize our bootstrap; strict-dynamic trusts scripts it loads.
        // Provider host wildcards consolidate Stripe.js and Intercom's script host families.
        csp.AllowScripts.FromSelf().AddNonce().WithStrictDynamic()
            .From("https://*.stripe.com")
            .From("https://*.intercom.io")
            .From("https://js.intercomcdn.com");

        // Read once when UseCsp configures the pipeline at startup, never per request.
        // Trust only hashes from the published SPA, never request or response HTML.
        // SvelteKit generates these for its bootstrap; static responses need no nonce rewriting.
        IFileInfo index = files.GetFileInfo("index.html");
        if (index.Exists)
        {
            using var reader = new StreamReader(index.CreateReadStream());
            foreach (Match hash in ScriptHashRegex().Matches(reader.ReadToEnd()))
                csp.AllowScripts.From(hash.Value);
        }

        // UI style attributes and Scalar/Intercom's injected styles still need inline CSS.
        csp.AllowStyles.FromSelf().AllowUnsafeInline();

        // Local previews, Stripe Link assets, core Intercom assets and user Gravatar images.
        csp.AllowImages.FromSelf().From("blob:").From("data:")
            .From("https://*.link.com")
            .From("https://js.intercomcdn.com")
            .From("https://static.intercomassets.com")
            .From("https://www.gravatar.com");
        // Bundled app fonts and Intercom's js/fonts CDN hosts.
        csp.AllowFonts.FromSelf().From("https://*.intercomcdn.com");

        // The backend serves a fixed policy; only Vite allows arbitrary environment connections.
        csp.AllowConnections.ToSelf()
            // Browser telemetry to Exceptionless collectors (hooks.client.ts).
            .To("https://*.exceptionless.io")
            // Payment Element uses Stripe.js; Link is enabled by its default payment options.
            .To("https://api.stripe.com")
            .To("https://link.com").To("https://*.link.com")
            // Messenger API/ping and realtime connections; no upload or attachment hosts.
            .To("https://*.intercom.io").To("wss://*.intercom.io")
            .To("https://*.intercom-messenger.com").To("wss://*.intercom-messenger.com");
        // Explicit configured WebSocket origins cover browsers where 'self' does not match WSS.
        // BaseURL/ApiUrl must match the externally served origins, including behind reverse proxies.
        // Invalid or missing origins add no sources; request/forwarded headers are never trusted.
        ApiContentSecurityPolicy.AllowConfiguredOrigins(csp, siteBaseUrl);
        ApiContentSecurityPolicy.AllowConfiguredOrigins(csp, apiUrl);

        // Stripe Payment Element, 3DS and Link frames.
        csp.AllowFrames.From("https://*.stripe.com")
            .From("https://link.com").From("https://*.link.com");

        // Intercom's core messenger sounds; optional video/attachment sources are excluded.
        csp.AllowAudioAndVideo.From("https://js.intercomcdn.com");

        // The SPA does not register workers; keep worker-src explicit because it otherwise
        // falls back through script-src rather than directly to default-src.
        csp.AllowWorkers.FromNowhere();
        csp.AllowFormActions.ToSelf();
        csp.AllowPlugins.FromNowhere();
        csp.AllowBaseUri.FromNowhere();
        csp.AllowFraming.FromNowhere();
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
