# Exceptionless
[![Build status](https://ci.appveyor.com/api/projects/status/4ept2hsism8bygls?svg=true)](https://ci.appveyor.com/project/Exceptionless/exceptionless) [![Gitter](https://badges.gitter.im/Join Chat.svg)](https://gitter.im/exceptionless/Discuss)

The definition of the word exceptionless is: to be without exception. [Exceptionless](http://exceptionless.com) provides real-time error reporting for your JavaScript, Node, ASP.NET, Web API, WebForms, WPF, Console, and MVC apps. It organizes the gathered information into simple actionable data that will help your app become exceptionless!

## Using Exceptionless
Refer to the [Exceptionless documentation wiki](https://github.com/exceptionless/Exceptionless/wiki/Getting-Started).

## Hosting Options
We provide very reasonably priced hosting at [Exceptionless](http://exceptionless.com). By using our hosted service, you are supporting the project and helping it get better! We also provide set up and support services.

If you would rather host Exceptionless yourself, you will need to follow the [self hosting documentation](https://github.com/exceptionless/Exceptionless/wiki/Self-Hosting).

## Contributing
_In appreciation for anyone who submits a non-trivial pull request, we will give you a free [Exceptionless](http://exceptionless.com) paid plan for a year. After your pull request is accepted, simply send an email to team@exceptionless.io with the name of your organization and we will upgrade you to a paid plan._

1. Please read the [contributing document](https://github.com/exceptionless/Exceptionless/blob/master/CONTRIBUTING.md).
2. Please follow the steps below to start configuring your Exceptionless development environment.
  1. You will need to have [Visual Studio 2015](http://www.visualstudio.com/products/visual-studio-community-vs) installed.
  2. Start `Elasticsearch` by running `StartBackendServers.bat`. *Please ensure that [dynamic scripting is enabled in the elasticsearch.yml file](https://github.com/exceptionless/Exceptionless/blob/master/Libraries/elasticsearch.yml#L12).*
  3. Open the `Exceptionless.sln` Visual Studio solution file.
  4. Select `Exceptionless.Api` as startup projects.
  5. Run the project by pressing `F5` to start the server.
  6. (OPTIONAL) For a user interface you must also setup and configure the [Exceptionless.UI](https://github.com/exceptionless/Exceptionless.UI) project.

Alternatively, you can [watch this short YouTube video](http://youtu.be/wROzlVuBoDs) showing how to get started with the project.

##  How is Exceptionless licensed?
The Exceptionless server is licensed under GNU AGPL v3.0. The client libraries are licensed under Apache License v2.0.

We want Exceptionless to be free for those of you who want to host the application and data internally or just simply do not want to pay for a hosted account. Our hope is that by making the application free and open source that more people will be aware of it and use it which will indirectly result in more people using our hosted service.

The server is licensed under the AGPL license to ensure that any modifications that are made will be contributed back to the community.

We chose to release the client libraries under Apache License v2.0 to remove any ambiguity as to the extent of the server license — you do not have to license any software that uses Exceptionless under AGPL and are completely free to use any licensing mechanism of your choice.

##Thanks
Thanks to the community for your support!

Thanks to [JetBrains](http://jetbrains.com) for a community [WebStorm](https://www.jetbrains.com/webstorm/) and [ReSharper](https://www.jetbrains.com/resharper/) license to use on this project. It's the best JavaScript IDE/Visual Studio productivity enhancement hands down.

Thanks to [Red Gate](http://www.red-gate.com) for providing an open source license for a [.NET Developer Bundle](http://www.red-gate.com/products/dotnet-development/). It's an indepensible tool when you need to track down a performance/memory issue.

Thanks to [OzCode](http://oz-code.com/) for providing an open source license to use on this project. OzCode adds a ton of value to the already amazing VS debugger.