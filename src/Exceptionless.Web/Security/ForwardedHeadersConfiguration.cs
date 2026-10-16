using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Exceptionless.Web.Security;

public static class ForwardedHeadersConfiguration
{
    public static void Configure(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        // ASP.NET Core owns this separate key and clears proxy trust when it is enabled.
        // https://learn.microsoft.com/dotnet/core/compatibility/aspnet-core/8.0/forwarded-headers-unknown-proxies
        if (String.Equals(configuration["ForwardedHeaders_Enabled"], "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The ASP.NET Core ForwardedHeaders_Enabled shortcut must not be enabled. Use the ForwardedHeaders configuration section to enable forwarding and configure trusted peers.");
        }

        var section = configuration.GetSection("ForwardedHeaders");
        options.ForwardedHeaders = section.GetValue("Enabled", true)
            ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            : ForwardedHeaders.None;
        options.RequireHeaderSymmetry = false;
        options.ForwardLimit = section.GetValue("ForwardLimit", 1);
        if (options.ForwardLimit is < 0)
        {
            throw new InvalidOperationException("ForwardedHeaders:ForwardLimit must be zero or positive.");
        }

        // Retain the framework's loopback defaults and add explicitly trusted ingress peers.
        foreach (var entry in GetEntries(section, "KnownProxies"))
        {
            if (!IPAddress.TryParse(entry.Value, out var address))
            {
                throw new InvalidOperationException($"{entry.Path} must be an IP address without a port.");
            }

            options.KnownProxies.Add(address);
        }

        foreach (var entry in GetEntries(section, "KnownNetworks"))
        {
            if (!System.Net.IPNetwork.TryParse(entry.Value, out var network))
            {
                throw new InvalidOperationException($"{entry.Path} must be a valid IPv4 or IPv6 CIDR.");
            }

            if (network.PrefixLength is 0)
            {
                throw new InvalidOperationException($"{entry.Path} must identify a trusted ingress network, not a /0 network.");
            }

            options.KnownIPNetworks.Add(network);
        }
    }

    private static IEnumerable<IConfigurationSection> GetEntries(IConfiguration configuration, string name)
    {
        var section = configuration.GetSection(name);
        if (section.Value is not null)
        {
            throw new InvalidOperationException($"{section.Path} must be a list. Use numbered environment keys such as EX_ForwardedHeaders__{name}__0.");
        }

        return section.GetChildren();
    }
}
