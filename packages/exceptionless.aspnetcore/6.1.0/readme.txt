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
      ASP.NET Core Integration
-------------------------------------
You must import the "Exceptionless" namespace and add the following code to register and configure the Exceptionless client:

using Exceptionless;

var builder = WebApplication.CreateBuilder(args); 
builder.Services.AddExceptionless("API_KEY_HERE");

In order to start gathering unhandled exceptions, you will need to register the Exceptionless middleware in your application 
like this after building your application:

var app = builder.Build(); 
app.UseExceptionless();

Alternatively, you can use different overloads of the AddExceptionless method for other configuration options.
Please visit the documentation at https://exceptionless.com/docs/clients/dotnet/sending-events/ for additional examples 
and guidance on sending events to Exceptionless.

-------------------------------------
   Manually reporting an exception
-------------------------------------
By default the Exceptionless Client will report all unhandled exceptions. You can
also manually send an exception by importing the Exceptionless namespace and calling
the following method.

exception.ToExceptionless().Submit()

Please note that ASP.NET Core doesn't have a static http context. We recommend registering
the http context accessor. Doing so will allow the request and user information to be populated.
You can do this by calling the AddHttpContextAccessor while configure services.

services.AddHttpContextAccessor()

-------------------------------------
      Documentation and Support
-------------------------------------
Please visit http://exceptionless.io for documentation and support.