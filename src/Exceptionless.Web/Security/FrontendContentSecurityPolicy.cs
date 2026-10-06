using System.Text.RegularExpressions;
using Joonasw.AspNetCore.SecurityHeaders.Csp.Builder;
using Microsoft.Extensions.FileProviders;

namespace Exceptionless.Web.Security;

internal static partial class FrontendContentSecurityPolicy
{
    public static void Configure(CspBuilder csp, IFileProvider files, bool upgradeInsecureRequests)
    {
        csp.ByDefaultAllow.FromSelf();
        csp.AllowScripts.FromSelf().AddNonce().WithStrictDynamic()
            .From("https://*.stripe.com")
            .From("https://*.intercom.io")
            .From("https://js.intercomcdn.com");

        // Trust only hashes from the published SPA, never request or response HTML.
        // SvelteKit generates these for its bootstrap; static responses need no nonce rewriting.
        IFileInfo index = files.GetFileInfo("index.html");
        if (index.Exists)
        {
            using var reader = new StreamReader(index.CreateReadStream());
            foreach (Match hash in ScriptHashRegex().Matches(reader.ReadToEnd()))
                csp.AllowScripts.From(hash.Value);
        }

        csp.AllowStyles.FromSelf().AllowUnsafeInline();
        csp.AllowImages.FromSelf().From("blob:").From("data:")
            .From("https://*.link.com")
            .From("https://js.intercomcdn.com")
            .From("https://static.intercomassets.com")
            .From("https://www.gravatar.com");
        csp.AllowFonts.FromSelf().From("https://*.intercomcdn.com");

        // ws: also permits wss:; * alone covers HTTP(S), not WebSockets.
        csp.AllowConnections.ToAnywhere().To("ws:");
        csp.AllowFrames.FromSelf().From("https://*.stripe.com")
            .From("https://link.com").From("https://*.link.com");
        csp.AllowAudioAndVideo.FromSelf().From("blob:").From("https://js.intercomcdn.com");
        csp.AllowWorkers.FromSelf();
        csp.AllowFormActions.ToSelf();
        csp.AllowManifest.FromSelf();
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
