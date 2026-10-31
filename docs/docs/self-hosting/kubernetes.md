---
title: "Kubernetes"
---

Please follow this section to set up Exceptionless in a Kubernetes environment. Please note this section is a work in progress, any contributions is greatly appreciated.

## Requirements

* [Kubernetes](https://kubernetes.io)
* [Helm](https://helm.sh)

## Instructions

Please note that we recommend you use Kubernetes for running in production. Configure durable, highly available Redis and Elasticsearch services for a production installation. The chart's bundled single-replica dependencies are intended for evaluation and do not provide a zero-downtime or durable production topology.

1. Follow the steps [here](https://github.com/exceptionless/Exceptionless/blob/master/k8s/ex-setup.ps1) for how to create it in AKS
2. View the configuration settings below for more information on configuring Exceptionless.
3. [Configure your clients](/docs/clients/) to send errors to your website.

Now, you can create a local account, organization, and project and send events to it.

## Configuration

The following section will cover how to configure exceptionless inside of a Kubernetes instance using the `exceptionless-config` config map. All exceptionless configuration keys are prefixed with `EX_`. Please note that these instructions also apply to docker using environment variables.

All configuration options and settings can be found in the various option classes located [here](https://github.com/exceptionless/Exceptionless/tree/master/src/Exceptionless.Core/Configuration).

The same `EX_` environment keys work in Docker Compose. Nested keys use double underscores.

### Trusted ingress peers

Before deploying behind ingress, configure [trusted forwarding peers](/docs/self-hosting/#reverse-proxies-and-https) through the Helm chart's existing `config` map. For example, after replacing the documentation address with a verified proxy source address:

```yaml
config:
  EX_ForwardedHeaders__KnownProxies__0: "192.0.2.10"
  EX_ForwardedHeaders__Enabled: "true"
  EX_ForwardedHeaders__ForwardLimit: "1"
```

For a dedicated ingress subnet with a single proxy hop, configure the chart's `forwardedHeaders.knownNetworks` list instead. Replace this example with your verified proxy subnet:

```yaml
forwardedHeaders:
  knownNetworks:
    - 192.0.2.0/24
```

The chart converts this list into indexed `EX_ForwardedHeaders__KnownNetworks__*` keys, enables forwarding, and sets a forward limit of one for both application hosts. Replacing the list removes obsolete subnet entries even with `--reuse-values`. Combining a nonempty list with manual `ForwardedHeaders` entries in `config` is rejected; remove conflicting entries from saved release values. The reusable chart's list is empty by default. For multiple proxy hops or other forwarding options, use explicit settings in `config` instead.

The hosted development and production values files explicitly trust `10.60.64.0/24`, the dedicated `ex-net-v6/ex-agc` association subnet for `kube-system/public-alb`. Update both files with any gateway subnet change. Deployments use these reviewed values without querying Azure for proxy trust. Self-hosted installations must configure their own proxy addresses.

The repository's Azure Application Gateway for Containers ingress terminates TLS and routes directly to application Services on port 80 and pods on port 8080. Verify the API-visible peer and any intermediate proxies before the first rollout and whenever the network changes. The ALB controller pod and Service address are not substitutes for the gateway's [association subnet](https://learn.microsoft.com/en-us/azure/application-gateway/for-containers/application-gateway-for-containers-components).

### Rollout checks and configuration failures

Before the first hosted rollout, verify the configured subnet against the gateway's association subnet and actual proxy path, and remove conflicting saved forwarding overrides. Repeat these checks when the network configuration changes. The separate framework `ForwardedHeaders_Enabled` shortcut must be absent or false.

| Failure | Result and recovery |
| --- | --- |
| Invalid application settings, such as a malformed IP, boolean, or `/0` network | New application processes fail at startup. Correct the configuration and redeploy. Production Helm waits up to five minutes for readiness and reports failure; it does not automatically roll back. |
| A valid CIDR does not contain the actual proxy, or forwarding is disabled | The app can remain ready while it ignores forwarded headers. Users can share the proxy's IP limits, and the request scheme reflects the backend connection. Readiness checks do not detect this. |
| The trusted range includes other callers | Those callers can supply forged client IPs or schemes. Use only the dedicated gateway subnet or verified proxy IPs. |

After the first authorized development rollout, verify client IPs, HTTPS detection, and rejection of spoofed forwarding headers before a production release. The existing version and readiness checks do not prove those behaviors.

Use the existing global-admin-only `GET /api/v2/admin/echo` endpoint for these checks. Its `ip_address` and `scheme` fields report the values the application sees after forwarded-header processing. Do not share the full echo response: its `headers` field can contain credentials and cookies.

| Step | Check | Pass condition |
| --- | --- | --- |
| Before merge | Required CI and configured gateway trust | CI passes; environment values match the verified dedicated gateway subnet and proxy path. Merging main starts the dev deployment. |
| Configuration | Saved Helm values, rendered ConfigMap, and new app/API pod environment | `EX_ForwardedHeaders__Enabled=true`, `EX_ForwardedHeaders__ForwardLimit=1`, and indexed `EX_ForwardedHeaders__KnownNetworks__*` match environment values. No conflicting forwarding overrides remain. Inspect only these keys, not unrelated secrets. |
| Rollout | Pod readiness, startup logs, dev migrations, and `/api/v2/about` | New app/API pods are ready, no configuration errors occur, migrations finish, and the expected version is served. |
| Baseline request | Call admin echo through HTTPS ingress with no forwarding or `X-Original-*` headers supplied by the client | `ip_address` equals the caller's known public egress IP and `scheme` is `https`. The baseline `X-Original-For` identifies a gateway peer within the configured subnet. Repeat on both app and API ingress. |
| Spoofed request | Repeat through the same ingress with `X-Forwarded-For: 203.0.113.123` and `X-Forwarded-Proto: http` | `ip_address` stays equal to the caller's actual egress IP and `scheme` stays `https`. Fail if either supplied value controls the result. |
| Direct untrusted request | In an authorized dev check, request the backend directly from a source outside trusted networks with the same fake headers | Forwarded values are ignored; `ip_address` is the direct peer and `scheme` is the backend transport scheme. Loopback or port-forwarding cannot prove this because loopback is trusted. |
| Production release | After dev passes, use the normal release-tag workflow and repeat baseline/spoof checks | Production serves the expected version with correct IP and scheme on both hosts. Stop promotion on any failed check; readiness alone is insufficient. |

For each request, retain only the observed IP, scheme, host, and pass/fail result. A global administrator can make the requests with their normal authenticated client. An unauthenticated request must still return `401`, and a non-admin request must return `403`.

For an authorized rollout, the following commands prompt for the administrator's password and print only the diagnostic fields. Set `BASE_URL` to the HTTPS ingress being checked and `ADMIN_EMAIL` to the administrator's email. Run the baseline first, then the spoof check; both must report the same real client IP and `scheme: "https"`.

```sh
# Baseline: do not supply forwarding or X-Original-* headers.
curl --fail --silent --show-error --user "$ADMIN_EMAIL" \
  "$BASE_URL/api/v2/admin/echo" \
  | jq '{ip_address, scheme, gateway_peer: .headers["X-Original-For"]}'

# Spoof check: these supplied values must not become the effective IP or scheme.
curl --fail --silent --show-error --user "$ADMIN_EMAIL" \
  --header 'X-Forwarded-For: 203.0.113.123' \
  --header 'X-Forwarded-Proto: http' \
  "$BASE_URL/api/v2/admin/echo" \
  | jq '{ip_address, scheme}'
```

## ConnectionStrings

See [Infrastructure Configuration](/docs/self-hosting/configuration) for the technology priority table, RabbitMQ examples, compatibility controls, Aspire naming, and rollout guidance. Existing Helm values remain supported without changes.

The current chart renders explicit legacy selectors for Cache, MessageBus, Queue, and Storage. Those selectors intentionally override automatic technology selection. Adding `EX_ConnectionStrings__RabbitMQ` under `config` alone therefore does not switch MessageBus. Use the existing Helm value:

```yaml
messagebus:
  connectionString: 'provider=rabbitmq;server="amqps://user:password@rabbitmq:5671/%2F"'
```

Keep every selector and effective endpoint unchanged during a rolling image upgrade. Changing a selector, provider, or endpoint is a separate infrastructure migration and requires a bridge or dual-read/write process, or a quiesce-and-drain maintenance window. Do not use `local` for a distributed role when multiple replicas may run; each replica would receive isolated in-memory state.

```yaml
# Redis automatically supplies Cache, MessageBus, and Queue when a higher-priority
# technology for a role is not configured.
EX_ConnectionStrings__Redis: localhost:6379,abortConnect=false

EX_ConnectionStrings__Elasticsearch: server=http://10.0.0.4:9200;
EX_ConnectionStrings__Email: smtps://user%40domain.com:password@smtp.domain.com:465
EX_ConnectionStrings__AzureQueues: DefaultEndpointsProtocol=https;AccountName=example;AccountKey=secret
EX_ConnectionStrings__AzureStorage: DefaultEndpointsProtocol=https;AccountName=example;AccountKey=secret
```

Structured connection strings support provider-specific key-value options. For example, you can control how many shards and replicas each Elasticsearch index should be created with by appending `shards=3;replicas=1` to `EX_ConnectionStrings__Elasticsearch`. Redis and RabbitMQ use their native complete connection-string formats.

## General Configuration

1. You'll want to set the `EX_ApiUrl` key to your external url of the api.
2. You'll want to set the `EX_BaseUrl` key to your external url of the website. If you are not following the clean urls optional section below, please make sure you also add the hashbang (`/#!`) to the base url.
3. `EX_AppMode` should be set to `Production` if you want to send unrestricted emails.
4. Please take a quick look at all the configuration options and settings that can be found in the various option classes located [here](https://github.com/exceptionless/Exceptionless/tree/master/src/Exceptionless.Core/Configuration).

## Active Directory Authentication

To enable Active Directory authentication, update the Update the `exceptionless-config` config map to include the `EX_ConnectionStrings__LDAP` connection string. The value should be your domain's LDAP URI (e.g. `LDAP://ad.domain.com/` or `LDAP://ad.domain.com/DC=domain,DC=com`).

Please note the following:

1. Users must still go through the registration process using their AD credentials. This allows account setup to proceed as normal. AD credentials are **not** stored.
2. Exceptionless relies on the following properties being available in AD:
    * `mail`: user's email address
    * `givenName`: user's first name
    * `sn`: user's last name
    * `sAMAccountName`: user's username
3. To ensure the correct account information is retrieved for a user, consider using a more specific connection string to narrow down the LDAP account type. For example: `LDAP://ad.domain.com/OU=Standard Users,OU=User Accounts,DC=domain,DC=com`

## Enabling Slack Integrations

1. Create a Slack app for your workspace
    * __Please do not distribute your app outside of your organization.__
2. Go to the `OAuth & Permissions` feature. Add a new redirect URL. The redirect URL should be your Exceptionless base URL.
3. On the *basic info* page of your Slack App, you will need to find the Client ID and Client Secret
4. Update the `exceptionless-config` config map `ConnectionStrings__OAuth` value to include `SlackId=YOUR_ID;SlackSecret=YOUR_SECRET;` and restart the associated pods.
5. If you've already loaded a page in Exceptionless, you will need to do a hard refresh for the config changes to apply.

## Upgrading

Please see the [Upgrading](/docs/self-hosting/upgrading-self-hosted-instance) for details on how to upgrade to the current version.

## Troubleshooting

If you are having issues please try the following to resolve the issues. If this doesn't work please open an issue or contact us on [Discord](https://discord.gg/xv3sjurVwA).

* Make sure you are running the latest release by visiting our [releases page](https://github.com/exceptionless/Exceptionless/releases). You can verify the version you are currently running by accessing the status page [`http://localhost/api/v2/about`](http://localhost/api/v2/about).
* You can also enable detailed logging by updating the `Serilog__MinimumLevel__Default` config map value to `Debug`.

---

[Next > Upgrading](/docs/self-hosting/upgrading-self-hosted-instance)
