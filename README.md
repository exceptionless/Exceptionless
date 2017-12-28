# Exceptionless
[![Build status](https://ci.appveyor.com/api/projects/status/4ept2hsism8bygls/branch/master?svg=true)](https://ci.appveyor.com/project/Exceptionless/exceptionless) [![Slack Status](https://slack.exceptionless.com/badge.svg)](https://slack.exceptionless.com) [![Donate](https://img.shields.io/badge/donorbox-donate-blue.svg)](https://donorbox.org/exceptionless?recurring=true) 

The definition of the word exceptionless is: to be without exception. [Exceptionless](http://exceptionless.com) provides real-time error reporting for your JavaScript, Node, .NET Core, ASP.NET, Web API, WebForms, WPF, Console, and MVC apps. It organizes the gathered information into simple actionable data that will help your app become exceptionless!

## Using Exceptionless
Refer to the [Exceptionless documentation wiki](https://github.com/exceptionless/Exceptionless/wiki/Getting-Started).

## Hosting Options
We provide very reasonably priced hosting at [Exceptionless](http://exceptionless.com). By using our hosted service, you are supporting the project and helping it get better! We also provide set up and support services.

If you would rather host Exceptionless yourself, you will need to follow the [self hosting documentation](https://github.com/exceptionless/Exceptionless/wiki/Self-Hosting). Also, if you want to support the project while self hosting you can send us a pull request or [donation](https://donorbox.org/exceptionless?recurring=true).

## Contributing
_In appreciation for anyone who submits a non-trivial pull request, we will give you a free [Exceptionless](http://exceptionless.com) paid plan for a year. After your pull request is accepted, simply send an email to team@exceptionless.io with the name of your organization and we will upgrade you to a paid plan._

1. Please read the [contributing document](https://github.com/exceptionless/Exceptionless/blob/master/CONTRIBUTING.md).
2. Please follow the steps below to start configuring your Exceptionless development environment.
  1. You will need to have an IDE like [Visual Studio Code](https://code.visualstudio.com) installed.
  2. Start `Elasticsearch` by running the `.\build\Start-Elasticsearch.ps1` [PowerShell script] (https://github.com/PowerShell/PowerShell#get-powershell).
  3. Open the `Exceptionless.sln` Visual Studio solution file.
  4. Select `Exceptionless.Web` as startup projects.
  5. Run the project by pressing `F5` to start the server.
  6. We have a prebuilt [Exceptionless UI](https://github.com/exceptionless/Exceptionless.UI) that's configured to talk to the localhost server instance. Just navigate to [http://local-app.exceptionless.io](http://local-app.exceptionless.io) to get started.

Alternatively, you can [watch this short YouTube video](http://youtu.be/wROzlVuBoDs) showing how to get started with the project.

## Thanks
Thanks to the community for your support!

Thanks to [JetBrains](http://jetbrains.com) for a community [WebStorm](https://www.jetbrains.com/webstorm/) and [ReSharper](https://www.jetbrains.com/resharper/) license to use on this project. It's the best JavaScript IDE/Visual Studio productivity enhancement hands down.

Thanks to [Red Gate](http://www.red-gate.com) for providing an open source license for a [.NET Developer Bundle](http://www.red-gate.com/products/dotnet-development/). It's an indepensible tool when you need to track down a performance/memory issue.
