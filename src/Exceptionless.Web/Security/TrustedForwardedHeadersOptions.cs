using System.Net;

namespace Exceptionless.Web.Security;

public sealed class TrustedForwardedHeadersOptions
{
    private const string FrameworkEnabledKey = "ForwardedHeaders_Enabled";

    public bool Enabled { get; private init; } = true;
    public int ForwardLimit { get; private init; } = 1;
    public IReadOnlyList<IPAddress> KnownProxies { get; private init; } = [];
    public IReadOnlyList<IPNetwork> KnownNetworks { get; private init; } = [];

    public static TrustedForwardedHeadersOptions ReadFromConfiguration(IConfiguration configuration)
    {
        // ASP.NET Core's separate shortcut clears proxy trust; it is not our nested Enabled setting.
        // https://github.com/dotnet/aspnetcore/blob/release/10.0/src/DefaultBuilder/src/ForwardedHeadersOptionsSetup.cs
        if (configuration.GetValue(FrameworkEnabledKey, false))
        {
            throw new InvalidOperationException($"The ASP.NET Core {FrameworkEnabledKey} shortcut must not be enabled. Use the ForwardedHeaders configuration section to enable forwarding and configure trusted peers.");
        }

        var section = configuration.GetSection("ForwardedHeaders");
        int forwardLimit = section.GetValue(nameof(ForwardLimit), 1);
        if (forwardLimit < 0)
        {
            throw new InvalidOperationException($"{section.Path}:{nameof(ForwardLimit)} must be zero or positive.");
        }

        return new TrustedForwardedHeadersOptions
        {
            Enabled = section.GetValue(nameof(Enabled), true),
            ForwardLimit = forwardLimit,
            KnownProxies = ReadKnownProxies(section),
            KnownNetworks = ReadKnownNetworks(section)
        };
    }

    private static IReadOnlyList<IPAddress> ReadKnownProxies(IConfigurationSection section)
    {
        var addresses = new List<IPAddress>();
        foreach (var entry in GetEntries(section, nameof(KnownProxies)))
        {
            if (!IPAddress.TryParse(entry.Value, out var address))
            {
                throw new InvalidOperationException($"{entry.Path} must be an IP address without a port.");
            }

            addresses.Add(address);
        }

        return addresses.AsReadOnly();
    }

    private static IReadOnlyList<IPNetwork> ReadKnownNetworks(IConfigurationSection section)
    {
        var networks = new List<IPNetwork>();
        foreach (var entry in GetEntries(section, nameof(KnownNetworks)))
        {
            if (!IPNetwork.TryParse(entry.Value, out var network))
            {
                throw new InvalidOperationException($"{entry.Path} must be a valid IPv4 or IPv6 CIDR.");
            }

            if (network.PrefixLength is 0)
            {
                throw new InvalidOperationException($"{entry.Path} must identify a trusted ingress network, not a /0 network.");
            }

            networks.Add(network);
        }

        return networks.AsReadOnly();
    }

    private static IEnumerable<IConfigurationSection> GetEntries(IConfigurationSection configuration, string name)
    {
        var section = configuration.GetSection(name);
        if (section.Value is not null)
        {
            throw new InvalidOperationException($"{section.Path} must be a list. Use numbered environment keys such as EX_ForwardedHeaders__{name}__0.");
        }

        return section.GetChildren();
    }
}
