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
		 Windows Integration
-------------------------------------
If your project has an app.config file, the Exceptionless NuGet package 
will automatically configure your app.config with the required config sections.
All you need to do is open the app.config and add your Exceptionless api key to 
the app.config Exceptionless section.

<exceptionless apiKey="API_KEY_HERE" />

If your project does not have an app.config file, then please add the following 
assembly attribute and your own Exceptionless api key to your project (E.G., AssemblyInfo class).

[assembly: Exceptionless.Configuration.Exceptionless("API_KEY_HERE")]

Finally, you must call the following line of code to start reporting unhandled exceptions.

Exceptionless.ExceptionlessClient.Current.Startup()

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