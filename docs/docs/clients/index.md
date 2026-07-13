---
title: "Clients"
---

# Clients

Exceptionless accepts events from any platform that can call its HTTP API. Start with an official SDK when one is available; it adds automatic error capture, event enrichment, queueing, and configuration support for its target runtime.

## Official SDKs

| Language or platform | Use it for | Get started | Source |
| --- | --- | --- | --- |
| .NET | ASP.NET Core, ASP.NET, console apps, services, WPF, Windows Forms, and .NET logging providers | [.NET client docs](/docs/clients/dotnet/) | [Exceptionless.Net](https://github.com/exceptionless/Exceptionless.Net) |
| JavaScript and TypeScript | Browsers, Node.js, Deno, React, Vue, AngularJS, React Native, and Expo | [JavaScript client docs](/docs/clients/javascript/) | [Exceptionless.JavaScript](https://github.com/exceptionless/Exceptionless.JavaScript) |
| Java | Java applications using the Maven package | [Java client repository](https://github.com/exceptionless/Exceptionless.Java) | [Exceptionless.Java](https://github.com/exceptionless/Exceptionless.Java) |

The JavaScript SDK is published as focused packages for each environment. Use the [JavaScript supported platforms matrix](/docs/clients/javascript/supported-platforms/) to choose the right package. React Native and Expo applications should start with the [React Native and Expo guide](/docs/clients/javascript/guides/react-native-expo/); Deno applications should use the [Deno guide](/docs/clients/javascript/guides/deno/).

## Additional integrations

- [Go client](https://github.com/exceptionless/Exceptionless.Go) — an Exceptionless client for Go applications. Review the repository's current compatibility and release status before adopting it in a new production application.
- [Swift example](https://github.com/exceptionless/Exceptionless-Swift-Example) — a sample showing how a Swift application can send events through the Exceptionless API; this is an example rather than a packaged SDK.
- [Serilog sink](https://github.com/exceptionless/serilog-sinks-exceptionless) — sends Serilog events to Exceptionless from .NET applications.
- [Custom clients](/docs/clients/custom-clients/) — use the HTTP API directly when your language or runtime does not have a dedicated SDK.

Not sure which path fits? [Open a client discussion](https://github.com/exceptionless/Exceptionless/discussions) and tell us your language, runtime, and deployment target.
