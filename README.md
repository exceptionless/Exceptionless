# Exceptionless [![Build Status](http://teamcity.codesmithtools.com/app/rest/builds/buildType:\(id:bt27\)/statusIcon)](http://teamcity.codesmithtools.com/project.html?projectId=Exceptionless)

The definition of the word exceptionless is: to be without exception. [Exceptionless](http://exceptionless.com) provides real-time .NET error reporting for your ASP.NET, Web API, WebForms, WPF, Console, and MVC apps. It organizes the gathered information into simple actionable data that will help your app become exceptionless!

## Requirements
1. Visual Studio 2013

## Getting Started

_** NOTE: If you simply want to use Exceptionless, just go to [http://exceptionless.com](http://exceptionless.com) and signup for a free account and you will be up and running in seconds._

1. Start MongoDB and Redis by opening `StartBackendServers.bat`.
2. Open the `Exceptionless.sln` Visual Studio solution file.
3. Select `Exceptionless.App` and `Exceptionless.SampleConsole` as startup projects.
4. Run the project.
5. The app will automatically make the 1st user that is created a Global Admin and will also create a sample `Acme` organization and project.
6. Send a test error from the sample console application and you should see it show up immediately in the website.

Alternatively, you can [watch this short YouTube video](http://youtu.be/wROzlVuBoDs) showing how to get started with the project.

## Using Exceptionless

Refer to the Exceptionless documentation here: [Exceptionless Docs](http://docs.exceptionless.com)

## Hosting Options

1. We provide very reasonably priced hosting at [Exceptionless](http://exceptionless.com). By using our hosted service, you are supporting the project and helping it get better!
2. If you would rather host Exceptionless yourself, you will need to follow these steps:
  1. Setup Mongo and Redis servers. We highly recommend that you run these on Linux systems because the Windows versions aren't as performant and reliable as the Linux versions. We also highly recommend that you setup Mongo in a replica set configuration.
  2. Setup IIS and add the Exceptionless website.
  3. Modify the connection strings in Web.config to point to your Mongo and Redis servers.
  4. Change the WebsiteMode to Production in the Web.config appSettings section.


##  How is Exceptionless licensed?

The Exceptionless server is licensed under GNU AGPL v3.0. The client libraries are licensed under Apache License v2.0.

We want Exceptionless to be free for those of you who want to host the application and data internally or just simply do not want to pay for a hosted account. Our hope is that by making the application free and open source that more people will be aware of it and use it which will indirectly result in more people using our hosted service.

The server is licensed under the AGPL license to ensure that any modifications that are made will be contributed back to the community.

We chose to release the client libraries under Apache License v2.0 to remove any ambiguity as to the extent of the server license — you do not have to license any software that uses Exceptionless under AGPL and are completely free to use any licensing mechanism of your choice.

## Roadmap

This is a list of high level things that we are planning to do:
- Refactor client so that the base client is a PCL library thus supporting WinRT and Mono.
- Implement search features using ElasticSearch.
- JavaScript client for reporting client side errors.
- Refactor the API and UI to be completely separate layers and rewrite the UI as a SPA app using AngularJS.
  - **We are looking for an AngularJS consultant to work on rewriting our UI layer.**
- Add a server side plugin system  that allows new functionality to be easily added like HipChat notifications.
