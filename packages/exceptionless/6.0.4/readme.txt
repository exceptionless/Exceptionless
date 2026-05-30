-------------------------------------
		Exceptionless Readme
-------------------------------------
Exceptionless provides real-time error reporting for your apps. It organizes the
gathered information into simple actionable data that will help your app become
exceptionless!

Learn more at http://exceptionless.io.

-------------------------------------
		How to get an api key
-------------------------------------
The Exceptionless client requires an api key to use the Exceptionless service.
You can get your Exceptionless api key by logging into http://exceptionless.io
and viewing your project configuration page.

-------------------------------------
		General Data Protection Regulation
-------------------------------------
By default the Exceptionless Client will report all available metadata including potential PII data.
You can fine tune the collection of information via Data Exclusions or turning off collection completely.

Please visit the documentation https://exceptionless.com/docs/clients/dotnet/private-information/
for detailed information on how to configure the client to meet your requirements.

-------------------------------------
		.NET Core Integration
-------------------------------------
This library is platform agnostic and is compiled against different runtimes. Depending on the
referenced runtime, Exceptionless will attempt to wire up to available error handlers and attempt to
discover configuration settings available to that runtime. For these reasons if you are on a known
platform then use the platform specific package to save you time configuring while giving you more
contextual information. For more information and configuration examples please read the Exceptionless
Configuration documentation on https://exceptionless.com/docs/clients/dotnet/configuration/

On app startup, import the Exceptionless namespace and call the client.Startup() extension method
to wire up to any runtime specific error handlers and read any available configuration.

Exceptionless.ExceptionlessClient.Default.Startup("API_KEY_HERE")

Please visit the documentation https://exceptionless.com/docs/clients/dotnet/sending-events/
for examples on sending events to Exceptionless.

-------------------------------------
		.NET Framework (Legacy) Integration
-------------------------------------
If your project has an app.config file, the Exceptionless NuGet package
will automatically configure your app.config with the required config sections.
All you need to do is open the app.config and add your Exceptionless api key to
the app.config Exceptionless section.

<exceptionless apiKey="API_KEY_HERE" />

If your project does not have an app.config file, then please add the following
assembly attribute and your own Exceptionless api key to your project (E.G., AssemblyInfo class).

[assembly: Exceptionless.Configuration.Exceptionless("API_KEY_HERE")]

Finally, you must import the Exceptionless namespace and call the following line
of code to start reporting unhandled exceptions.

Exceptionless.ExceptionlessClient.Default.Startup()

Please visit the documentation https://exceptionless.com/docs/clients/dotnet/sending-events/
for examples on sending events to Exceptionless.

-------------------------------------
   Manually reporting an exception
-------------------------------------
By default the Exceptionless Client will report all unhandled exceptions. You can
also manually send an exception by importing the Exceptionless namespace and calling
the following method.

exception.ToExceptionless().Submit()

-------------------------------------
		Documentation and Support
-------------------------------------
Please visit http://exceptionless.io for documentation and support.
