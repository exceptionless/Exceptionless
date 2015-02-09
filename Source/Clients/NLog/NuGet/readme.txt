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
		 NLog Integration
-------------------------------------
If your project has an app.config file, the Exceptionless.NLog NuGet package 
will automatically configure your app.config with the required config sections.
All you need to do is open the app.config and add your Exceptionless api key to 
the app.config Exceptionless section.

<exceptionless apiKey="API_KEY_HERE" />

If your project does not have an app.config file, then please add the following 
assembly attribute and your own Exceptionless api key to your project (E.G., AssemblyInfo class).

[assembly: Exceptionless.Configuration.Exceptionless("API_KEY_HERE")]

Next, you must add a Exceptionless NLog target and configure the logger rules to output to exceptionless.

  <extensions>
    <add assembly="Exceptionless.NLog"/>
  </extensions>

  <targets>
    <target xsi:type="ColoredConsole" name="console" />

    <target name="exceptionless"  xsi:type="Exceptionless">
      <field name="host" layout="${machinename}" />
      <field name="identity" layout="${identity}" />
      <field name="windows-identity" layout="${windows-identity:userName=True:domain=False}" />
      <field name="process" layout="${processname}" />
    </target>
  </targets>

  <rules>
    <logger name="*" minlevel="Trace" writeTo="console,exceptionless" />
  </rules>

-------------------------------------
	  Documentation and Support
-------------------------------------
Please visit http://exceptionless.io for documentation and support.