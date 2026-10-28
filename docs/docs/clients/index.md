---
title: "Choose a client"
description: "Choose an Exceptionless client for your language, see each client's install command and setup, or send events from any language over HTTP."
---

# Choose a client

A client library captures errors and other events in your app and sends them to Exceptionless. Pick your language below
to see the package to install and the setup code, then follow the link to its full guide.

```clients
```

## Official clients {#official-clients}

- [.NET](/docs/clients/dotnet/) for ASP.NET Core, worker services, console and desktop apps, Blazor, serverless
  functions, and the common logging frameworks.
- [JavaScript](/docs/clients/javascript/) for browsers, Node.js, React, React Native and Expo, Next.js, SvelteKit, Vue,
  Angular, and Express.

[Client features](/docs/clients/features/) compares what each client supports.

## Support levels {#support-levels}

Every client listed here works with the current version of Exceptionless. Each one has a support level:

- **Official** clients are maintained by the Exceptionless team and documented on this site.
- **Community** clients are built and maintained by other developers. Report problems in the client's own repository.
- **Preview** clients are new and work today, but their API may still change before a stable release.

Clients that don't work with the current version aren't listed.

## Any other language {#other-languages}

You don't need a client library to use Exceptionless. Any language or script that can make an HTTPS request can send
events to the REST API with a client API key. See [Other languages and custom clients](/docs/clients/custom-clients/)
for Bash, PowerShell, Python, and Go examples.

## Write a client {#write-a-client}

The [client specification](/docs/reference/client-spec/) describes what every Exceptionless client does, such as
batching, the offline queue, retries, configuration values, and sessions. Follow it to build an official or community
client for a new language.
