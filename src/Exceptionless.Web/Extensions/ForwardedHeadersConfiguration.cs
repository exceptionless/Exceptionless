using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Exceptionless.Web.Extensions;

public static class ForwardedHeadersConfiguration
{
    public static void Configure(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.RequireHeaderSymmetry = false;
        options.ForwardLimit = configuration.GetValue("ForwardedHeaders:ForwardLimit", 1);
        if (options.ForwardLimit < 1)
            throw new InvalidOperationException("ForwardedHeaders:ForwardLimit must be positive.");

        // Retain the framework's loopback defaults and add explicitly trusted ingress peers.
        foreach (string address in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
            options.KnownProxies.Add(IPAddress.Parse(address));
        foreach (string value in configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
        {
            var network = System.Net.IPNetwork.Parse(value);
            if (network.PrefixLength == 0)
                throw new InvalidOperationException("ForwardedHeaders:KnownNetworks must identify trusted ingress networks.");
            options.KnownIPNetworks.Add(network);
        }
    }
}
