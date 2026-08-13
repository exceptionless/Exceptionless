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

_Please note that if you are specifying configuration via `docker-compose`, then you will need to drop the `EX_` and `EX_ConnectionStrings__` prefixes._

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
