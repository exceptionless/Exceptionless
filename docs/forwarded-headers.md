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
