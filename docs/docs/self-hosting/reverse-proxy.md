---
title: "Reverse proxies and HTTPS"
navTitle: "Reverse proxies"
metaTitle: "Run self-hosted Exceptionless behind a reverse proxy"
description: "Put nginx, Caddy, or a load balancer in front of self-hosted Exceptionless and trust it, so client IP addresses and HTTPS are detected."
sources:
  - src/Exceptionless.Web/Security/TrustedForwardedHeadersOptions.cs
  - src/Exceptionless.Web/Security/ForwardedHeadersConfiguration.cs
  - src/Exceptionless.Web/Program.cs
  - src/Exceptionless.Web/appsettings.yml
  - src/Exceptionless.Web/Extensions/HttpExtensions.cs
  - src/Exceptionless.Web/Utility/Handlers/ThrottlingMiddleware.cs
  - src/Exceptionless.Web/Api/Handlers/AdminHandler.cs
  - src/Exceptionless.Web/Api/Endpoints/AssistantEndpoints.cs
  - src/Exceptionless.Web/Api/Endpoints/SourceMapEndpoints.cs
  - k8s/exceptionless/templates/config.yaml
---

# Reverse proxies and HTTPS

Most installations put a reverse proxy, such as nginx, Caddy, or a cloud load balancer, in front of Exceptionless to
serve HTTPS. This page shows how to tell Exceptionless which proxies to trust, so it sees each visitor's real IP address
and knows the request came in over HTTPS.

## How Exceptionless reads forwarded headers

A proxy passes the visitor's address in `X-Forwarded-For` and the original scheme in `X-Forwarded-Proto`. Anyone can
send those headers, so Exceptionless reads them only from loopback addresses and from proxies you list.

When the proxy isn't trusted, every request appears to come from the proxy's address over plain HTTP. All anonymous
visitors then share one set of IP-based limits, including API rate limits, sign-up limits, and OAuth client registration
limits.

Exceptionless doesn't read `X-Forwarded-Host` or `X-Forwarded-Prefix`. Its public URLs come from `EX_BaseURL` and
`EX_ApiUrl`, so set those to the URLs people use, and serve Exceptionless at the root of its host name.

You don't need any of this if browsers and clients connect to Exceptionless directly.

## Trust your proxy {#trust-your-proxy}

1. **Find the proxy's address as Exceptionless sees it.** This is the source address of the connection that reaches the
   Exceptionless container, which isn't always the proxy's public address. Sign in as a global administrator, then call
   the echo endpoint through your proxy. Until the proxy is trusted, `ip_address` is the proxy's address:

   ```bash
   curl --silent --user admin@example.com https://errors.example.com/api/v2/admin/echo | jq '{ip_address, scheme}'
   ```

   curl asks for your password. The full response also lists your request headers, including credentials, so don't share
   it.

2. **Add the address to the trusted list** on the `app` and `api` containers:

   ```yaml
   EX_ForwardedHeaders__KnownProxies__0: "192.0.2.10"
   ```

   Add more addresses as `__1`, `__2`, and so on. If the proxy's address changes, trust its network instead, but only a
   network that nothing else can connect from:

   ```yaml
   EX_ForwardedHeaders__KnownNetworks__0: "192.0.2.0/28"
   ```

3. **Restart Exceptionless.** It reads these settings only at startup.

4. **Check the result.** Call the echo endpoint again. `ip_address` should now be your own public IP address and
   `scheme` should be `https`.

5. **Check that visitors can't fake it.** Send a made-up address. `ip_address` must still be your real address:

   ```bash
   curl --silent --user admin@example.com -H "X-Forwarded-For: 203.0.113.123" https://errors.example.com/api/v2/admin/echo | jq '{ip_address, scheme}'
   ```

You can also set these in `appsettings.Local.yml` in the app folder:

```yaml
ForwardedHeaders:
  KnownProxies:
    - 192.0.2.10
```

## Settings

| Environment variable                    | Default | What it does                                                                                                       |
| --------------------------------------- | ------- | ------------------------------------------------------------------------------------------------------------------ |
| `EX_ForwardedHeaders__Enabled`          | `true`  | Reads `X-Forwarded-For` and `X-Forwarded-Proto` from trusted proxies. Set to `false` to ignore them from everyone. |
| `EX_ForwardedHeaders__ForwardLimit`     | `1`     | How many proxies to read through, starting from the one closest to Exceptionless. `0` ignores forwarded headers.   |
| `EX_ForwardedHeaders__KnownProxies__0`  | None    | A trusted proxy IP address, without a port. Loopback addresses are always trusted.                                 |
| `EX_ForwardedHeaders__KnownNetworks__0` | None    | A trusted proxy network in CIDR form, such as `192.0.2.0/24`. IPv4 and IPv6 both work.                             |

Exceptionless refuses to start if the settings are invalid:

- Addresses with ports or host names instead of IP addresses.
- A `/0` network, which would trust everyone.
- A list written as one comma-separated value instead of numbered keys.
- A negative `ForwardLimit`.
- ASP.NET Core's separate `ForwardedHeaders_Enabled` shortcut set to `true`, for example as the
  `EX_ForwardedHeaders_Enabled` environment variable. That shortcut clears the trusted lists. Use the settings above
  instead.

Environment variables override YAML key by key. Setting `KnownProxies__0` in the environment replaces only the first
YAML entry, so remove old entries from YAML rather than relying on an override.

### More than one proxy

If requests pass through several proxies, such as a CDN and then a load balancer, set `ForwardLimit` to the number of
proxies and trust every one of them. Exceptionless reads `X-Forwarded-For` from right to left and stops when it reaches
a proxy it doesn't trust.

### Troubleshooting

To log why a forwarded header was ignored, raise the log level for ASP.NET Core's forwarded headers middleware and
restart:

```yaml
EX_Serilog__MinimumLevel__Override__Microsoft.AspNetCore.HttpOverrides: Debug
```

## What the proxy needs to do {#what-the-proxy-needs-to-do}

- **Set the forwarded headers itself.** The proxy that faces the internet must replace any `X-Forwarded-For` it receives
  with the client's address, or append the client's address to it, and set `X-Forwarded-Proto` from its own connection.
- **Allow WebSockets** on `/api/v2/push`. The web UI gets real-time updates there.
- **Allow large uploads.** Source map uploads can be up to 20 MB.
- **Allow long responses.** Exie streams its answers for up to two minutes. Exceptionless sends `X-Accel-Buffering: no`
  on those responses so nginx doesn't buffer them.

## nginx

This example runs nginx on the same server as the [Docker Compose setup](/docs/self-hosting/docker/), with the app
published on port 7110:

```nginx
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}

server {
    listen 443 ssl;
    server_name errors.example.com;

    ssl_certificate     /etc/ssl/certs/errors.example.com.pem;
    ssl_certificate_key /etc/ssl/private/errors.example.com.key;

    client_max_body_size 25m;

    location / {
        proxy_pass http://127.0.0.1:7110;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $remote_addr;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_read_timeout 180s;
    }
}
```

`X-Forwarded-For $remote_addr` replaces whatever the client sent, so nginx must be the first proxy.

Because nginx reaches the container through a published Docker port, Exceptionless usually sees the Docker network's
gateway address, not `127.0.0.1`. Use the echo check above to find it. To keep that address the same when you recreate
the containers, give the Compose network a fixed subnet:

```yaml
networks:
  default:
    ipam:
      config:
        - subnet: 172.30.0.0/24
```

## Caddy in Docker Compose

Caddy gets HTTPS certificates automatically, sets `X-Forwarded-For` and `X-Forwarded-Proto` itself, ignores forwarded
headers from clients by default, and passes WebSockets and streamed responses through. Save this as `conf/Caddyfile`
next to your Compose file:

```text
errors.example.com {
    reverse_proxy app:8080
}
```

Run it in the same Compose project as Exceptionless with a fixed address, and trust that address. The `ip_range` keeps
Docker from giving that address to another container. Add this to the [Compose file](/docs/self-hosting/docker/):

```yaml
services:
  caddy:
    image: caddy:2
    ports:
      - 80:80
      - 443:443
      - 443:443/udp
    volumes:
      - ./conf:/etc/caddy
      - caddy_data:/data
      - caddy_config:/config
    networks:
      default:
        ipv4_address: 172.30.0.10
    restart: unless-stopped

networks:
  default:
    ipam:
      config:
        - subnet: 172.30.0.0/24
          ip_range: 172.30.0.128/25

volumes:
  caddy_data:
  caddy_config:
```

Then add these to the shared Exceptionless settings, and remove the app's `ports` entry so all traffic goes through
Caddy:

```yaml
EX_BaseURL: https://errors.example.com
EX_ExceptionlessServerUrl: https://errors.example.com
EX_EnableSsl: "true"
EX_ForwardedHeaders__KnownProxies__0: "172.30.0.10"
```

## Kubernetes

In Kubernetes, the proxy is your ingress controller or gateway. The Helm chart can trust a dedicated ingress subnet with
`forwardedHeaders.knownNetworks`. See [Kubernetes](/docs/self-hosting/kubernetes/#trust-your-ingress).
