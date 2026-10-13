Webhooks use HTTP or HTTPS destinations. Connections resolve and check the destination address at delivery time, including URLs saved before deployment. Redirects are not followed, and requests do not use a proxy or cookies.

For a self-hosted private receiver, explicitly configure its smallest required network on every API and job host:

```yaml
WebHooks:
  AllowedPrivateNetworks:
    - 10.20.30.40/32
```

The default list is empty. Existing private receivers require this override before deploying. Their saved records are preserved. Delivery logs identify the destination origin; target paths, queries, credentials, and HTTP delivery traces are omitted.
