# Exceptionless [![Build Status](http://teamcity.codesmithtools.com/app/rest/builds/buildType:\(id:Exceptionless_Master\)/statusIcon)](http://teamcity.codesmithtools.com/viewType.html?buildTypeId=Exceptionless_Master) [![Gitter](https://badges.gitter.im/Join Chat.svg)](https://gitter.im/exceptionless/Exceptionless?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

The definition of the word exceptionless is: to be without exception. [Exceptionless](http://exceptionless.com) provides real-time .NET error reporting for your ASP.NET, Web API, WebForms, WPF, Console, and MVC apps. It organizes the gathered information into simple actionable data that will help your app become exceptionless!

***
 
_This branch is for [Exceptionless 2.0] (http://github.com/exceptionless/Exceptionless/wiki/Exceptionless-2.0-Overview) which is a work in progress. Please switch to the 1.x branch if you are looking for the stable version._

***

## Getting Started

_** NOTE: If you simply want to use Exceptionless, just go to [http://exceptionless.com](http://exceptionless.com) and signup for a free account and you will be up and running in seconds._

1. You will need to have Visual Studio 2013 installed.
2. Start MongoDB and Redis by opening `StartBackendServers.bat`.
3. Open the `Exceptionless.sln` Visual Studio solution file.
4. Select `Exceptionless.Api.IIS` and `Exceptionless.SampleConsole` as startup projects.
5. Run the project.
6. The app will automatically make the 1st user that is created a Global Admin and will also create a sample `Acme` organization and project.
7. Send a test error from the sample console application and you should see it show up immediately in the website.

Alternatively, you can [watch this short YouTube video](http://youtu.be/wROzlVuBoDs) showing how to get started with the project.

## Using Exceptionless

Refer to the Exceptionless documentation here: [Exceptionless Docs](http://docs.exceptionless.com)

## Hosting Options

1. We provide very reasonably priced hosting at [Exceptionless](http://exceptionless.com). By using our hosted service, you are supporting the project and helping it get better! We also provide set up and support services.
2. If you would rather host Exceptionless yourself, you will need to follow these steps:
  1. Setup Mongo ([Linux](http://docs.mongodb.org/manual/administration/install-on-linux/), [Windows](http://docs.mongodb.org/manual/tutorial/install-mongodb-on-windows/)) and Redis servers  ([Linux](http://redis.io/download), [Windows] (https://github.com/MSOpenTech/redis)). We highly recommend that you run these on Linux systems because the Windows versions aren't as performant and reliable as the Linux versions. We also highly recommend that you setup Mongo in a replica set configuration.
  2. Setup IIS and add the Exceptionless website.
  3. Modify the connection strings in Web.config to point to your Mongo and Redis servers.
  4. Change the WebsiteMode to Production in the Web.config appSettings section.
  5. [Configure your clients](http://docs.exceptionless.com/contents/configuration/#self-hosted-options) to send errors to your installation.


##  How is Exceptionless licensed?

The Exceptionless server is licensed under GNU AGPL v3.0. The client libraries are licensed under Apache License v2.0.

We want Exceptionless to be free for those of you who want to host the application and data internally or just simply do not want to pay for a hosted account. Our hope is that by making the application free and open source that more people will be aware of it and use it which will indirectly result in more people using our hosted service.

The server is licensed under the AGPL license to ensure that any modifications that are made will be contributed back to the community.

We chose to release the client libraries under Apache License v2.0 to remove any ambiguity as to the extent of the server license — you do not have to license any software that uses Exceptionless under AGPL and are completely free to use any licensing mechanism of your choice.

## Contributing

Please read the [contributing document](https://github.com/exceptionless/Exceptionless/blob/master/CONTRIBUTING.md).

In appreciation for anyone who submits a non-trivial pull request, we will give you a free [Exceptionless](http://exceptionless.com) paid plan for a year. After your pull request is accepted, simply send an email to team@exceptionless.com with the name of your organization and we will upgrade you to a paid plan.

## Roadmap

This is a list of high level things that we are planning to do:
- ~~Refactor client so that the base client is a PCL library thus supporting WinRT and Mono. **(Completed)**~~
- ~~Refactor the API to be MUCH simpler and allow for clients to be easily developed while at the same time making the entire sytem much more flexible and able to gather additional data like log messages and feature usage. **(Completed)**~~
- ~~Implement search features using ElasticSearch. **(Completed)**~~
- JavaScript client for reporting client side errors.
- Refactor the API and UI to be completely separate layers and rewrite the UI as a SPA app using AngularJS. **(In Progress)**
- Add a server side plugin system  that allows new functionality to be easily added like HipChat notifications. **(In Progress)**


##Thanks
Thanks to the community for your support!

Thanks to [JetBrains](http://jetbrains.com) for a community [WebStorm](https://www.jetbrains.com/webstorm/) license to use on this project. It's the best JavaScript IDE hands down.

Thanks to [Red Gate](http://www.red-gate.com) for providing an open source license for a [.NET Developer Bundle](http://www.red-gate.com/products/dotnet-development/). It's an indepensible tool when you need to track down a performance/memory issue.
