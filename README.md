# Exceptionless [![Build Status](http://teamcity.codesmithtools.com/app/rest/builds/buildType:\(id:bt27\)/statusIcon)](http://teamcity.codesmithtools.com/project.html?projectId=Exceptionless)

The definition of the word exceptionless is: to be without exception. [Exceptionless](http://exceptionless.com) provides real-time .NET error reporting for your ASP.NET, Web API, WebForms, WPF, Console, and MVC apps. It organizes the gathered information into simple actionable data that will help your app become exceptionless!

#### Please follow the steps below when using exceptionless in an development environment.

1. Start MongoDB and Redis by opening powershell and executing `.\StartBackendServers.ps1`.
2. Open the `Exceptionless.sln` Visual Studio solution file.
3. Select the `Exceptionless.Web` website as the startup project.
4. Start debugging.

#### Self-Hosted Installs
We highly recommend running Redis and Mongo on Linux as they are not fully featured or very stable on Windows.