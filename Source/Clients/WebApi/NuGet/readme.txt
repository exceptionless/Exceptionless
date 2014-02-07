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
		 ASP.NET Web Api Integration
-------------------------------------
The Exceptionless.WebApi package will automatically configure your web.config. 
All you need to do is open the web.config and add your Exceptionless api key to 
the web.config Exceptionless section.

<exceptionless apiKey="API_KEY_HERE" />

Finally, you must import the "Exceptionless" namespace and call the following line
of code to start reporting unhandled exceptions. You will need to pass an
HttpConfiguration instance.

Exceptionless.ExceptionlessClient.Current.RegisterWebApi(config)

If you are hosting Web API inside of ASP.NET, you would register Exceptionless like:

Exceptionless.ExceptionlessClient.Current.RegisterWebApi(GlobalConfiguration.Configuration)

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