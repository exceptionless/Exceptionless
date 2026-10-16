using Microsoft.AspNetCore.HttpOverrides;

namespace Exceptionless.Web.Security;

public static class ForwardedHeadersConfiguration
{
    public static void Configure(ForwardedHeadersOptions options, TrustedForwardedHeadersOptions settings)
    {
        options.ForwardedHeaders = settings.Enabled
            ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            : ForwardedHeaders.None;
        options.RequireHeaderSymmetry = false;
        options.ForwardLimit = settings.ForwardLimit;

        // Retain the framework's loopback defaults and add explicitly trusted ingress peers.
        foreach (var address in settings.KnownProxies)
        {
            options.KnownProxies.Add(address);
        }

        foreach (var network in settings.KnownNetworks)
        {
            options.KnownIPNetworks.Add(network);
        }
    }
}
