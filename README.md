# Exceptionless
[![Build](https://github.com/exceptionless/Exceptionless/workflows/Build/badge.svg?branch=master)](https://github.com/exceptionless/Exceptionless/actions?query=branch%3Amaster)
[![Discord](https://img.shields.io/discord/715744504891703319)](https://discord.gg/6HxgFCx)
[![Donate](https://img.shields.io/badge/donorbox-donate-blue.svg)](https://donorbox.org/exceptionless?recurring=true)

The definition of the word exceptionless is: to be without exception. [Exceptionless](http://exceptionless.com) provides real-time error reporting for your JavaScript, Node, .NET Core, ASP.NET, Web API, WebForms, WPF, Console, and MVC apps. It organizes the gathered information into simple actionable data that will help your app become exceptionless!

⭐️ We appreciate your star, it helps!

## Using Exceptionless
Refer to the [Exceptionless documentation](https://exceptionless.com/docs/).

## Hosting Options
We provide very reasonably priced hosting at [Exceptionless](http://exceptionless.com). By using our hosted service, you are supporting the project and helping it get better! We also provide set up and support services.

Exceptionless can be run locally as simply as `docker run --rm -it -p 5000:80 exceptionless/exceptionless:latest`. This will run a completely self-contained simple instance of Exceptionless. It is only suitable for testing purposes since it will not persist data. For more complete setups, check out the [self hosting documentation](https://exceptionless.com/docs/self-hosting/). Also, if you want to support the project while self hosting you can send us a pull request or [donation](https://donorbox.org/exceptionless?recurring=true).

## Contributing
_In appreciation for anyone who submits a non-trivial pull request, we will give you a free [Exceptionless](http://exceptionless.com) paid plan for a year. After your pull request is accepted, simply send an email to team@exceptionless.io with the name of your organization and we will upgrade you to a paid plan._

- Please read the [contributing document](https://github.com/exceptionless/Exceptionless/blob/master/CONTRIBUTING.md).
- Please follow the steps below to start configuring your Exceptionless development environment.
  - Make sure you have [Visual Studio Code](https://code.visualstudio.com) installed. You can also use Visual Studio or JetBrains Rider, but these steps assume you are using Visual Studio Code.
  - Make sure you have [Docker](https://www.docker.com/get-docker) installed.
  - Open Visual Studio Code and then open the Exceptionless root folder.
  - Go to the `Tasks` menu and select `Run Task...` and then select `Start Elasticsearch`.
  - Go to the `Debug` menu and select `Start Debugging`.
  - Open the `exceptionless.http` file in VS Code to begin making requests to the API. Make sure that you have the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension installed.
  - If you want to use the Exceptionless UI then go to the `Tasks` menu and select `Run Task...` and then select `Start Exceptionless UI` and open a browser to `http://ex-ui.localtest.me:5100`.
  - When running locally in `Development` mode, a global administrator user `test@test.com` is automatically created with password `tester`.

## Thanks

Thanks to all the people who have contributed!

[![contributors](https://contributors-img.web.app/image?repo=exceptionless/exceptionless)](https://github.com/exceptionless/exceptionless/graphs/contributors)

Thanks to [JetBrains](http://jetbrains.com) for a community [WebStorm](https://www.jetbrains.com/webstorm/) and [ReSharper](https://www.jetbrains.com/resharper/) license to use on this project. It's the best JavaScript IDE/Visual Studio productivity enhancement hands down.

Thanks to [Red Gate](http://www.red-gate.com) for providing an open source license for a [.NET Developer Bundle](http://www.red-gate.com/products/dotnet-development/). It's an indispensible tool when you need to track down a performance/memory issue.
