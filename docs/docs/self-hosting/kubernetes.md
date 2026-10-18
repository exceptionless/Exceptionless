---
title: "Kubernetes"
---

Please follow this section to set up Exceptionless in a Kubernetes environment. Please note this section is a work in progress, any contributions is greatly appreciated.

## Requirements

* [Kubernetes](https://kubernetes.io)
* [Helm](https://helm.sh)

## Instructions

Please note that we recommend you use Kubernetes for running in production.

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

The repository's ingress templates use Azure Application Gateway for Containers, terminate TLS at ingress, and route `/` to the API Service on port 80 and container port 8080. The hosted development and production deployment workflow discovers the gateway's association subnet before each Helm upgrade, so subnet addresses do not need to be duplicated in environment values files.

The shared `k8s/scripts/discover-forwarded-headers.js` script requires Node 24 LTS or later. It renders the environment's API ingress locally, reads the selected `ApplicationLoadBalancer` through Kubernetes, and resolves its association subnet IDs through Azure. It requires the existing deployment identity to have Kubernetes read access to that resource and Azure `Microsoft.Network/virtualNetworks/subnets/read` access to its subnets. Missing associations, failed reads, missing `Microsoft.ServiceNetworking/trafficControllers` delegation, invalid prefixes, and unrestricted `/0` prefixes stop deployment.

Discovery also requires the gateway's `Accepted` and `Deployment` conditions to be `True`, with each `observedGeneration` equal to `metadata.generation`. A missing, stale, or deleting gateway stops discovery before Helm changes the application. Wait for the ALB controller to finish and rerun deployment. See Microsoft's [successful gateway provisioning example](https://learn.microsoft.com/en-us/azure/application-gateway/for-containers/quickstart-create-application-gateway-for-containers-managed-by-alb-controller#validate-creation-of-the-application-gateway-for-containers-resources).

Discovery writes a `forwardedHeaders.knownNetworks` Helm list. The chart converts it into indexed `EX_ForwardedHeaders__KnownNetworks__*` keys, enables forwarding, and sets a forward limit of one for both application hosts. Replacing the list removes obsolete subnet entries even with `--reuse-values`. Combining discovery with manual `ForwardedHeaders` entries in `config` is rejected; remove those old entries from saved release values before using discovery.

For manual upgrades, run discovery from the repository root after selecting the intended Kubernetes context and Azure subscription, then pass the generated file **last** to Helm. For example, the development commands are:

```sh
node k8s/scripts/discover-forwarded-headers.js ex-dev k8s/ex-dev-values.yaml /tmp/ex-dev-forwarded-headers.json
# Run only if discovery succeeded:
helm upgrade ex-dev k8s/exceptionless --namespace ex-dev --reuse-values \
  --values k8s/ex-dev-values.yaml --values /tmp/ex-dev-forwarded-headers.json
```

For production, use `ex-prod`, `k8s/ex-prod-values.yaml`, and a separate output file. The script only reads infrastructure and replaces the local values file; Helm performs the upgrade. It removes any previous output before discovery so a failed refresh cannot leave stale trust available for reuse. Automated workflows stop on discovery failure; manual operators must also stop.

This assumes the gateway connects directly to the application. Verify the API-visible peer and any intermediate proxies before the first rollout; discovering Azure configuration does not prove the live traffic path. The ALB controller pod and Service address are not substitutes for the association subnet. Other ingress topologies should use explicit trusted peers in `config` as shown above. See Microsoft's [association subnet documentation](https://learn.microsoft.com/en-us/azure/application-gateway/for-containers/application-gateway-for-containers-components).

### Rollout checks and configuration failures

For hosted deployments, check the deployment identity's read permissions and the actual proxy path once per environment before the first rollout. Remove conflicting saved forwarding overrides if present. Repeat these checks when the deployment identity or network topology changes. Each subsequent deployment discovers the current subnet automatically; no copied subnet values are required.

| Failure | Result and recovery |
| --- | --- |
| Discovery cannot read a ready gateway or a valid subnet | The workflow stops before the application Helm upgrade. Fix permissions or wait for reconciliation, then rerun. Earlier development infrastructure steps may already have run. |
| Invalid application settings, such as a malformed IP, boolean, or `/0` network | New application processes fail at startup. Correct the configuration and redeploy. Production Helm waits up to five minutes for readiness and reports failure; it does not automatically roll back. |
| A valid CIDR does not contain the actual proxy, or forwarding is disabled | The app can remain ready while it ignores forwarded headers. Users can share the proxy's IP limits, and the request scheme reflects the backend connection. Readiness checks do not detect this. |
| The trusted range includes other callers | Those callers can supply forged client IPs or schemes. Use only the dedicated gateway subnet or verified proxy IPs. |

After the first authorized development rollout, verify client IPs, HTTPS detection, and rejection of spoofed forwarding headers before a production release. The existing version and readiness checks do not prove those behaviors.

Use the existing global-admin-only `GET /api/v2/admin/echo` endpoint for these checks. Its `ip_address` and `scheme` fields report the values the application sees after forwarded-header processing. Do not share the full echo response: its `headers` field can contain credentials and cookies.

| Step | Check | Pass condition |
| --- | --- | --- |
| Before merge | Required CI and deployment identity permissions | CI passes; the deployment identity can read the selected gateway and its association subnets. Merging main starts the dev deployment. |
| Discovery | Gateway conditions and generated `forwardedHeaders.knownNetworks` | Current-generation `Accepted` and `Deployment` conditions are true. Generated CIDRs match the dedicated gateway subnets. Discovery enforces readiness, identity, delegation, and prefix validation. |
| Configuration | Saved Helm values, rendered ConfigMap, and new app/API pod environment | `EX_ForwardedHeaders__Enabled=true`, `EX_ForwardedHeaders__ForwardLimit=1`, and indexed `EX_ForwardedHeaders__KnownNetworks__*` match discovery. No conflicting forwarding overrides remain. Inspect only these keys, not unrelated secrets. |
| Rollout | Pod readiness, startup logs, dev migrations, and `/api/v2/about` | New app/API pods are ready, no configuration errors occur, migrations finish, and the expected version is served. |
| Baseline request | Call admin echo through HTTPS ingress with no forwarding or `X-Original-*` headers supplied by the client | `ip_address` equals the caller's known public egress IP and `scheme` is `https`. The baseline `X-Original-For` identifies a gateway peer within the discovered subnet. Repeat on both app and API ingress. |
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

```yaml
# connection string used for any provider specifying Redis.
EX_ConnectionStrings__Redis: localhost:6379,abortConnect=false

EX_ConnectionStrings__Cache: provider=redis;
EX_ConnectionStrings__Elasticsearch: server=http://10.0.0.4:9200;
EX_ConnectionStrings__Email: smtps://user%40domain.com:password@smtp.domain.com:465
EX_ConnectionStrings__MessageBus: provider=redis;
EX_ConnectionStrings__Metrics: provider=statsd;server=localhost
EX_ConnectionStrings__Queue: provider=redis;
EX_ConnectionStrings__Storage: provider=azurestorage;
```

You can append values to any connection string using a `;`. For example, you can control many shards and replicas each Elasticsearch index should be created with by appending to the `EX_ConnectionStrings__Elasticsearch` connection string. For a Elasticsearch cluster (3 nodes, two masters), you would append `shards=3;replicas=1`.

The `provider` value determines what implementations to use for the various abstractions. We've made it easier to reuse a single connection string by automatically looking up a connection string by the provider name and adding any key value pairs to the current connection string (as shown above with redis).

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
