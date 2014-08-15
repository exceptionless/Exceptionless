-------------------------------------
		Exceptionless Readme
-------------------------------------
Exceptionless provides real-time error reporting for your apps. It organizes the 
gathered information into simple actionable data that will help your app become 
exceptionless!

Learn more at http://exceptionless.com.

-------------------------------------
		How to get an api key
-------------------------------------
The Exceptionless client requires an api key to use the Exceptionless service. 
You can get your Exceptionless api key by logging into http://exceptionless.com 
and viewing your project configuration page.

-------------------------------------
		 ASP.NET Integration
-------------------------------------
The Exceptionless.Web NuGet package will automatically configure your web.config.
To start reporting unhandled exceptions in your application, add your Exceptionless 
api key to the web.config Exceptionless section.

<exceptionless apiKey="API_KEY_HERE" />

You can also add support for WCF by adding the ExceptionlessWcfHandleError 
attribute to your WCF classes.

[ExceptionlessWcfHandleError]

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
Please visit http://exceptionless.com for documentation and support.