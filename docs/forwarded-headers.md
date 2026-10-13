The API uses forwarded client addresses and schemes only from trusted peers. Framework loopback defaults remain enabled; non-loopback ingress peers must be configured before deployment.

Configure the actual proxy addresses or their smallest required network and the number of forwarding hops:

```yaml
ForwardedHeaders:
  KnownProxies:
    - 10.20.30.40
  KnownNetworks:
    - 10.20.40.0/24
  ForwardLimit: 1
```

Leave unused lists empty. The ingress must replace incoming forwarded headers rather than preserve client-supplied values. Public access should terminate at that ingress. With no additional configuration, headers from other peers are ignored. IPv4-mapped IPv6 client addresses use the same login window as their IPv4 equivalent.

## Compatibility and deployment

This changes the previous trust-all behavior. Before upgrading a deployment behind a non-loopback proxy, configure the peer addresses as seen by the API and verify the actual forwarding chain. Otherwise, forwarded client IPs and HTTPS schemes are ignored: IP-based login, signup, password-reset and anonymous API limits can group users behind the proxy, and scheme-dependent redirects can behave incorrectly. Authenticated API throttling still uses its existing user or organization identifier. No endpoint routes, payload schemas, API keys, or WebSocket message formats change.

Direct self-hosted installations do not need a proxy or additional configuration. `ForwardLimit: 1` is the maximum number of forwarded entries processed, not a minimum number of proxies required on each request. Requests without forwarded headers retain their connection IP and scheme. Multiple trusted proxies require a limit matching the intended chain and trust configuration for each traversed proxy.

The current application configuration requires a positive integer. An explicit zero or negative limit prevents the middleware from initializing. This is stricter than ASP.NET Core itself: zero processes no forwarded entries, and a nullable limit can allow unlimited depth. This application does not currently expose the unlimited option. Supporting an explicit disabled mode or unlimited depth should be a separate, tested configuration decision; unlimited depth still requires restricted trusted peers.

Forwarded headers run after exception/status-code handling and before HTTPS redirection, request logging, routing, MCP origin validation, authentication, and throttling. This lets downstream middleware use the validated client IP and scheme. Microsoft explicitly permits placement after diagnostics and error handling. See [Microsoft's proxy configuration guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

Before rollout, verify direct access, a trusted proxy, an untrusted peer, and the deployed multi-proxy chain in staging. Check the resulting client IP, HTTPS scheme, origin checks, and IP-based limits. The unit tests do not establish the SaaS ingress topology or validate a self-hoster's proxy configuration.
