using System.Net;
using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public sealed class WebHookOptions
{
    public IPNetwork[] AllowedPrivateNetworks { get; internal set; } = [];

    public static WebHookOptions ReadFromConfiguration(IConfiguration configuration)
        => new()
        {
            AllowedPrivateNetworks = configuration.GetSection("WebHooks:AllowedPrivateNetworks").Get<string[]>()?.Select(IPNetwork.Parse).ToArray() ?? []
        };
}
