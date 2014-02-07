# Exceptionless [![Build Status](http://teamcity.codesmithtools.com/app/rest/builds/buildType:\(id:bt27\)/statusIcon)](http://teamcity.codesmithtools.com/project.html?projectId=Exceptionless)

The definition of the word exceptionless is: to be without exception. [Exceptionless](http://exceptionless.com) provides real-time .NET error reporting for your ASP.NET, Web API, WebForms, WPF, Console, and MVC apps. It organizes the gathered information into simple actionable data that will help your app become exceptionless!

## Getting Started

1. Start MongoDB and Redis by opening PowerShell and executing `.\StartBackendServers.ps1`.
2. Open the `Exceptionless.sln` Visual Studio solution file.
3. Select the `Exceptionless.Web` and `Exceptionless.SampleConsole` as startup projects.
4. Run the project.
5. The app will automatically make the 1st user that is created a Global Admin and will also create a sample `Acme` organization and project.
6. Send a test error from the sample console application and you should see it show up immediately in the website.

## Using Exceptionless

Refer to the Exceptionless documentation here: [Exceptionless Docs](http://docs.exceptionless.com)

## Hosting Options

1. We provide very reasonably priced hosting at [Exceptionless](http://exceptionless.com). By using our hosted service, you are supporting the project and helping it get better!
2. If you would rather host Exceptionless yourself, you will need to follow these steps:
  1. Setup Mongo and Redis servers. We highly recommend that you run these on Linux systems because the Windows versions aren't as performant and reliable as the Linux versions. We also highly recommend that you setup Mongo in a replica set configuration.
  2. Setup IIS and add the Exceptionless website.
  3. Modify the connection strings in Web.config to point to your Mongo and Redis servers.
  4. Change the WebsiteMode to Production in the Web.config appSettings section.
