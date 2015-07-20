# Exceptionless
[![Build status](https://ci.appveyor.com/api/projects/status/4ept2hsism8bygls?svg=true)](https://ci.appveyor.com/project/Exceptionless/exceptionless) [![Gitter](https://badges.gitter.im/Join Chat.svg)](https://gitter.im/exceptionless/Discuss)

The definition of the word exceptionless is: to be without exception. [Exceptionless](http://exceptionless.io) provides real-time error reporting for your JavaScript, Node, ASP.NET, Web API, WebForms, WPF, Console, and MVC apps. It organizes the gathered information into simple actionable data that will help your app become exceptionless!

## Using Exceptionless

Refer to the Exceptionless documentation here: [Exceptionless Docs](http://docs.exceptionless.io)

## Hosting Options

1. We provide very reasonably priced hosting at [Exceptionless](http://exceptionless.com). By using our hosted service, you are supporting the project and helping it get better! We also provide set up and support services.
2. If you would rather host Exceptionless yourself, you will need to follow these steps:
  1. Setup `Elasticsearch` ([Linux](http://www.elasticsearch.org/guide/en/elasticsearch/reference/current/setup-service.html), [Windows](http://www.elasticsearch.org/guide/en/elasticsearch/reference/current/setup-service-win.html)) with our [`elasticsearch.yml`](https://github.com/exceptionless/Exceptionless/blob/master/Libraries/elasticsearch.yml) config and `Redis `servers  ([Linux](http://redis.io/download), [Windows](https://github.com/MSOpenTech/redis)). We highly recommend that you run these on Linux systems because the Windows versions aren't as performant and reliable as the Linux versions.
  2. Setup IIS and add the Exceptionless API website ([Download](https://github.com/exceptionless/Exceptionless/releases)).
  3. Update the connection strings in the `Web.config` file to point to your `Elasticsearch` and `Redis` servers.
  4. Update the app settings (`BaseURL`, `EnableSSL`, `WebsiteMode`, etc..) in the `Web.config` file.
  5. Update the mail settings in the `Web.config` file.
  6. [Configure your clients](http://docs.exceptionless.io/contents/configuration/#self-hosted-options) to send errors to your installation.

## Contributing

Please read the [contributing document](https://github.com/exceptionless/Exceptionless/blob/master/CONTRIBUTING.md).

In appreciation for anyone who submits a non-trivial pull request, we will give you a free [Exceptionless](http://exceptionless.io) paid plan for a year. After your pull request is accepted, simply send an email to team@exceptionless.io with the name of your organization and we will upgrade you to a paid plan.

## Getting Started (Development)

_NOTE: If you simply want to use Exceptionless, just go to [http://exceptionless.io](http://exceptionless.io) and signup for a free account and you will be up and running in seconds._

**This section is for development purposes only! If you are trying to self host Exceptionless, than please refer to the `Hosting Options` section above.**

1. You will need to have [Visual Studio 2013](http://www.visualstudio.com/products/visual-studio-community-vs) installed.
2. Start `Elasticsearch` by running `StartBackendServers.bat`. *Please ensure that [dynamic scripting is enabled in the elasticsearch.yml file](https://github.com/exceptionless/Exceptionless/blob/master/Libraries/elasticsearch.yml#L12).*
3. Open the `Exceptionless.sln` Visual Studio solution file.
4. Select `Exceptionless.Api` as startup projects.
5. Run the project by pressing `F5` to start the server.
6. (OPTIONAL) For a user interface you must also setup and configure the [Exceptionless.UI](https://github.com/exceptionless/Exceptionless.UI) project.

Alternatively, you can [watch this short YouTube video](http://youtu.be/wROzlVuBoDs) showing how to get started with the project.

## Roadmap

We are currently in the planning process for 3.1. However, this is a list of high level things that we are planning to do:
- Bug fixes **(In Progress)**
- New Dashboards
- vnext support
- [Let us know what you'd like us to work on!](https://github.com/exceptionless/Exceptionless/issues)

##  How is Exceptionless licensed?

The Exceptionless server is licensed under GNU AGPL v3.0. The client libraries are licensed under Apache License v2.0.

We want Exceptionless to be free for those of you who want to host the application and data internally or just simply do not want to pay for a hosted account. Our hope is that by making the application free and open source that more people will be aware of it and use it which will indirectly result in more people using our hosted service.

The server is licensed under the AGPL license to ensure that any modifications that are made will be contributed back to the community.

We chose to release the client libraries under Apache License v2.0 to remove any ambiguity as to the extent of the server license — you do not have to license any software that uses Exceptionless under AGPL and are completely free to use any licensing mechanism of your choice.

##Thanks
Thanks to the community for your support!

Thanks to [JetBrains](http://jetbrains.com) for a community [WebStorm](https://www.jetbrains.com/webstorm/) and [ReSharper](https://www.jetbrains.com/resharper/) license to use on this project. It's the best JavaScript IDE/Visual Studio productivity enhancement hands down.

Thanks to [Red Gate](http://www.red-gate.com) for providing an open source license for a [.NET Developer Bundle](http://www.red-gate.com/products/dotnet-development/). It's an indepensible tool when you need to track down a performance/memory issue.

Thanks to [OzCode](http://oz-code.com/) for providing an open source license to use on this project. It adds a ton of value to the already amazing VS debugger.