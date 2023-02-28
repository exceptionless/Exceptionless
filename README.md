[![Exceptionless](https://raw.githubusercontent.com/exceptionless/MediaKit/main/exceptionless-dark-bg.svg#gh-dark-mode-only "Exceptionless")](https://exceptionless.com#gh-dark-mode-only)
[![Exceptionless](https://raw.githubusercontent.com/exceptionless/MediaKit/main/exceptionless.svg#gh-light-mode-only "Exceptionless")](https://exceptionless.com#gh-light-mode-only)

[![Build](https://github.com/exceptionless/Exceptionless/workflows/Build/badge.svg?branch=main)](https://github.com/exceptionless/Exceptionless/actions?query=branch%3Amain)
[![Discord](https://img.shields.io/discord/715744504891703319)](https://discord.gg/6HxgFCx)
[![Donate](https://img.shields.io/badge/donorbox-donate-blue.svg)](https://donorbox.org/exceptionless?recurring=true)

The definition of the word exceptionless is: to be without exception. [Exceptionless](http://exceptionless.com) provides real-time error reporting for your JavaScript, Node, .NET Core, ASP.NET, Web API, WebForms, WPF, Console, and MVC apps. It organizes the gathered information into simple actionable data that will help your app become exceptionless!

⭐️ We appreciate your star, it helps!

## Using Exceptionless
Refer to the [Exceptionless documentation](https://exceptionless.com/docs/).

## Hosting Options
We provide very reasonably priced hosting at [Exceptionless](http://exceptionless.com). By using our hosted service, you are supporting the project and helping it get better! We also provide set up and support services.

Exceptionless can be run locally as simply as `docker run --rm -it -p 5200:80 exceptionless/exceptionless:latest`. This will run a completely self-contained simple instance of Exceptionless. It is only suitable for testing purposes since it will not persist data. For more complete setups, check out the [self hosting documentation](https://exceptionless.com/docs/self-hosting/). Also, if you want to support the project while self hosting you can send us a pull request or [donation](https://donorbox.org/exceptionless?recurring=true).

## Contributing
_In appreciation for anyone who submits a non-trivial pull request, we will give you a free [Exceptionless](http://exceptionless.com) paid plan for a year. After your pull request is accepted, simply send an email to team@exceptionless.io with the name of your organization and we will upgrade you to a paid plan._

- Please read the [contributing document](https://github.com/exceptionless/Exceptionless/blob/main/CONTRIBUTING.md)
- Requirements
  - [Docker](https://www.docker.com/get-docker)
  - [.NET 7.0](https://dotnet.microsoft.com/)
  - [Node 18+](https://nodejs.org/)
- Visual Studio Code
  - Open Visual Studio Code and then open the Exceptionless root folder
  - Go to the `Terminal` menu and select `Run Task...` and then select `Start Elasticsearch`
  - Go to the `Debug` menu and select the `Web` launch configuration then click the `Start Debugging` button
  - A browser window should be automatically opened to `https://localhost:5100/`
  - When running locally in `Development` mode, a global administrator user `test@localhost` is automatically created with password `tester`. You can also click the `Signup` button to create a new account
- Visual Studio
  - Open Visual Studio and then open the `Exceptionless.sln` solution in the root folder
  - Start Elasticsearch by either configuring multiple startup projects for the `docker-compose` and `Exceptionless.Web` projects or by running the `start-services.ps1` script in the root folder
  - Run the `Exceptionless.Web` project
  - A browser window should be automatically opened to `https://localhost:5100/`
  - When running locally in `Development` mode, a global administrator user `test@localhost` is automatically created with password `tester`. You can also click the `Signup` button to create a new account

## UI Only Development
The UI is a SPA application that runs against the Exceptionless API. The source is located in the `src/Exceptionless.Web/ClientApp` folder. The UI will automatically be started when running the whole project, but if you want to work on just the API, then open Visual Studio Code to the `src/Exceptionless.Web/ClientApp` folder and run the `npm run serve (use exceptionless api)` task to start the UI pointing at the official Exceptionless API. You will need to login to your actual Exceptionless account.

## API Only Development
You can work on just the API without running the SPA UI by selecting the `Exceptionless API` launch configuration in Visual Studio. You can then run requests using the `exceptionless.http` file. Make sure that you have the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension installed.

## Thanks

Thanks to all the people who have contributed!

[![contributors](https://contributors-img.web.app/image?repo=exceptionless/exceptionless)](https://github.com/exceptionless/exceptionless/graphs/contributors)

Thanks to [JetBrains](http://jetbrains.com) for a community [WebStorm](https://www.jetbrains.com/webstorm/) and [ReSharper](https://www.jetbrains.com/resharper/) license to use on this project. It's the best JavaScript IDE/Visual Studio productivity enhancement hands down.

Thanks to [Red Gate](http://www.red-gate.com) for providing an open source license for a [.NET Developer Bundle](http://www.red-gate.com/products/dotnet-development/). It's an indispensible tool when you need to track down a performance/memory issue.
