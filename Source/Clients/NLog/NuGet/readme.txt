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

Here is an example NLog.config file that shows how to use the Exceptionless NLog target.

<nlog>
  <extensions>
    <add assembly="Exceptionless.NLog"/>
  </extensions>
  
  <targets async="true">
    <target name="exceptionless" xsi:type="Exceptionless">
      <field name="host" layout="${machinename}" />
      <field name="identity" layout="${identity}" />
      <field name="windows-identity" layout="${windows-identity:userName=True:domain=False}" />
      <field name="process" layout="${processname}" />
    </target>
  </targets>
  
  <rules>
    <logger name="*" minlevel="Info" writeTo="exceptionless" />
  </rules>
</nlog>

-------------------------------------
	  Documentation and Support
-------------------------------------
Please visit http://exceptionless.io for documentation and support.