---
title: "Self Hosting"
---

# Self Hosting

Exceptionless can be self-hosted by either manually running the source code for the server and the frontend or by using our simply Docker image.

You can also use Kubernetes while self-hosting Exceptionless. We'll cover both topics in the following pages.

* [Docker](/docs/self-hosting/docker)
* [Kubernetes](/docs/self-hosting/kubernetes)
* [Upgrading](/docs/self-hosting/upgrading-self-hosted-instance)

## Reverse proxies and HTTPS

Direct HTTP/HTTPS hosting needs no forwarded-header configuration. The API accepts `X-Forwarded-For` and `X-Forwarded-Proto` only from loopback or explicitly trusted proxies. A known proxy is the **source IP of the connection reaching the API**, not its hostname, public frontend IP, Kubernetes Service IP, or proxy identifier. For Docker bridges or NAT, verify the address seen inside the API container.

Before upgrading from unrestricted forwarding, configure every non-loopback proxy that must supply client addresses or HTTPS schemes. Otherwise requests use the proxy IP and backend scheme: unrelated users can share IP-based limits and HTTPS detection can be incorrect. Set the public website `EX_BaseURL` and API `EX_ApiUrl` separately; forwarded headers do not configure public origins.

Use this section in the web application's `appsettings.Local.yml`, replacing the example address with the verified proxy source IP:

```yaml
ForwardedHeaders:
  Enabled: true
  ForwardLimit: 1
  KnownProxies:
    - 192.0.2.10
```

The equivalent Docker Compose `environment` entries, or Helm values under the chart's existing `config` map, are:

```yaml
EX_ForwardedHeaders__Enabled: "true"
EX_ForwardedHeaders__ForwardLimit: "1"
EX_ForwardedHeaders__KnownProxies__0: "192.0.2.10"
```

For changing proxy addresses, use `KnownNetworks` **instead of** `KnownProxies` with a verified, dedicated proxy CIDR, for example `EX_ForwardedHeaders__KnownNetworks__0: "192.0.2.0/24"`. Do not trust the whole cluster, Docker bridge, or private address space when other workloads can connect from it. Use network access controls to restrict backend access. IPv4 and IPv6 addresses/CIDRs are supported; IPv4 entries also match IPv4-mapped IPv6 peers. Use canonical network CIDRs; hostnames, ports, comma-separated scalar lists, malformed entries, and `/0` networks are rejected at startup.

All supported Exceptionless settings belong under `ForwardedHeaders`. `Enabled` defaults to `true`; set `ForwardedHeaders:Enabled` to `false` (environment variable `EX_ForwardedHeaders__Enabled=false`) to leave client addresses and schemes unchanged. Exceptionless reads and validates this section once at startup into `TrustedForwardedHeadersOptions`, then configures ASP.NET Core's `ForwardedHeadersOptions` from those typed values through [.NET's options registration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0). ASP.NET Core does not automatically bind this section. The group and `Enabled` switch are Exceptionless settings.

`ForwardLimit` and `KnownProxies` match [ASP.NET Core's forwarded-header options](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0#forwarded-headers-middleware-options). The `KnownNetworks` configuration key retains the older framework name; Exceptionless parses its CIDR strings into .NET 10's [`KnownIPNetworks` property](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.forwardedheadersoptions.knownipnetworks?view=aspnetcore-10.0). Both address lists are parsed and validated explicitly while preserving the framework's loopback defaults. Boolean settings use .NET's typed `GetValue` conversion; malformed booleans fail startup.

`ForwardLimit` defaults to one entry, processed from right to left. Multiple proxies require a finite limit matching the intended chain and trust for each traversed peer. Processing stops at an unknown peer even with unused hop budget. Zero also disables processing for compatibility; negative or non-integer limits are invalid.

Do not set the separate framework variable `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`. [Microsoft documents that this shortcut clears the trusted-peer lists](https://learn.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/8.0/forwarded-headers-unknown-proxies). ASP.NET Core exposes it internally as [`ForwardedHeaders_Enabled`](https://github.com/dotnet/aspnetcore/blob/release/10.0/src/DefaultBuilder/src/ForwardedHeadersOptionsSetup.cs#L21); Exceptionless rejects the active shortcut at startup. This framework key is not part of the Exceptionless configuration group and cannot replace `ForwardedHeaders:Enabled`.

The public edge proxy must derive the scheme from its connection and replace untrusted forwarding headers, or append the observed client address so the configured hop limit cannot reach user-supplied entries. Configure Nginx, Caddy, Traefik, or other proxies accordingly. Loopback proxies and IIS out-of-process integration normally need no additional trust entries. A remote IIS/ARR proxy needs the same explicit peer configuration as other remote proxies.

Environment variables override YAML by key; command-line values override both. Use double underscores for nesting and numbered entries for lists. Overriding index `0` does not remove other entries inherited from YAML; remove obsolete trust entries from their source and restart after changes. Avoid defining the same key with both prefixed and unprefixed environment names.

The API processes IP/scheme headers before redirect, authentication, and throttling middleware. It does not consume `X-Forwarded-Host` or `X-Forwarded-Prefix`; preserve the public Host and root paths. Verify HTTPS, client IPs, redirects, and IP-based limits through the actual proxy chain before rollout. Enable `EX_Serilog__MinimumLevel__Override__Microsoft.AspNetCore.HttpOverrides: "Debug"` temporarily to diagnose unknown peers. See [Microsoft's proxy guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

---

[Next > Docker](/docs/self-hosting/docker)
