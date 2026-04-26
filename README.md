[![Exceptionless](https://raw.githubusercontent.com/exceptionless/MediaKit/main/exceptionless-dark-bg.svg#gh-dark-mode-only "Exceptionless")](https://exceptionless.com#gh-dark-mode-only)
[![Exceptionless](https://raw.githubusercontent.com/exceptionless/MediaKit/main/exceptionless.svg#gh-light-mode-only "Exceptionless")](https://exceptionless.com#gh-light-mode-only)

[![Build](https://github.com/exceptionless/Exceptionless/workflows/Build/badge.svg?branch=main)](https://github.com/exceptionless/Exceptionless/actions?query=branch%3Amain)
[![Discord](https://img.shields.io/discord/715744504891703319)](https://discord.gg/6HxgFCx)
[![BuiltWithDot.Net shield](https://builtwithdot.net/project/100/exceptionless-csharp-error-reporting/badge)](https://builtwithdot.net/project/100/exceptionless-csharp-error-reporting)

The definition of the word exceptionless is: to be without exception. [Exceptionless](https://exceptionless.com) provides real-time error reporting for your JavaScript, Node, .NET Core, ASP.NET, Web API, WebForms, WPF, Console, and MVC apps. It organizes the gathered information into simple actionable data that will help your app become exceptionless!

⭐️ We appreciate your star, it helps!

## Using Exceptionless

Refer to the [Exceptionless documentation](https://exceptionless.com/docs/).

## Hosting Options

We provide very reasonably priced hosting at [Exceptionless](https://exceptionless.com). By using our hosted service, you are supporting the project and helping it get better! We also provide set up and support services.

Exceptionless can easily be run locally using Docker:

- `docker run --rm -it -p 7110:8080 exceptionless/exceptionless:latest`
- Open `http://localhost:7110`
- Create an account. The first account in the system will automatically be an admin.

This will run a completely self-contained simple instance of Exceptionless. It is only suitable for testing purposes since it will not persist data. For more complete setups, check out the [self hosting documentation](https://exceptionless.com/docs/self-hosting/). Also, if you want to support the project while self hosting you can send us a pull request or [donation](https://github.com/sponsors/exceptionless).

## Contributing

_In appreciation for anyone who submits a non-trivial pull request, we will give you a free [Exceptionless](https://exceptionless.com) paid plan for a year. After your pull request is accepted, simply send an email to <team@exceptionless.io> with the name of your organization and we will upgrade you to a paid plan._

Start here:

1. Read the [contributing document](https://github.com/exceptionless/Exceptionless/blob/main/CONTRIBUTING.md).
2. Install [Docker](https://www.docker.com/get-docker), [.NET 10.0](https://dotnet.microsoft.com/), and [Node 24+](https://nodejs.org/).
3. Run the app with one of these entry points:
    - Visual Studio Code: open the repo root and start the `Aspire` launch configuration.
    - Visual Studio: open `Exceptionless.slnx`, set `Exceptionless.AppHost` as the startup project, and run it.
    - CLI or Dev Container: run `aspire run` from the repo root.

After startup:

1. Open `https://localhost:7121/` if a browser does not open automatically.
2. In `Development` mode, a global administrator user `test@localhost` with password `tester` is created automatically.

Notes:

1. Running `Exceptionless.AppHost` starts the app and required infrastructure together.
2. Backend tests bootstrap required infrastructure automatically.

![image](https://user-images.githubusercontent.com/282584/223168564-6518d509-d292-4078-a61f-ab493d2bb812.png)

## UI Development

Frontend work currently spans two apps:

1. The legacy Angular UI in `src/Exceptionless.Web/ClientApp.angular` is still the main site UI. Most of that app lives in `app/`, `components/`, `less/`, `img/`, `lang/`, and `grunt/`.
2. The Svelte 5 UI in `src/Exceptionless.Web/ClientApp` is still under development.

For examples of API requests, see `exceptionless.http`. If you use that file in Visual Studio Code, install the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension.

## Thanks

Thanks to all the people who have contributed!

[![contributors](https://contributors-img.web.app/image?repo=exceptionless/exceptionless)](https://github.com/exceptionless/exceptionless/graphs/contributors)

Thanks to [JetBrains](https://jetbrains.com) for a community [WebStorm](https://www.jetbrains.com/webstorm/) and [ReSharper](https://www.jetbrains.com/resharper/) license to use on this project. It's the best JavaScript IDE/Visual Studio productivity enhancement hands down.

Thanks to [Red Gate](https://www.red-gate.com) for providing an open source license for a [.NET Developer Bundle](https://www.red-gate.com/products/dotnet-development/). It's an indispensable tool when you need to track down a performance/memory issue.
