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
		 Integration
-------------------------------------
You can set your api key by calling the following code on startup:

ExceptionlessClient.Default.Configuration.ApiKey = "API_KEY_HERE"

You can also configure the client via attributes. To configure the client using attributes please add 
the following assembly attribute and your own Exceptionless api key to your project (E.G., AssemblyInfo class).

[assembly: Exceptionless.Configuration.Exceptionless("API_KEY_HERE")]

Finally, you must call the following line of code to read your configuration from the attribute.

Exceptionless.ExceptionlessClient.Default.Configuration.ReadFromAttributes()

Please note that you will need to pass in the the assembly that contains the attributes if you place
place the above attribute outside of the  entry assembly or calling assembly.

Exceptionless.ExceptionlessClient.Default.Configuration.ReadFromAttributes(typeof(MyClass).Assembly)

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
