-------------------------------------
		Exceptionless Readme
-------------------------------------
Exceptionless provides real-time error reporting for your apps. It organizes the 
gathered information into simple actionable data that will help your app become 
exceptionless!

Learn more at http://exceptionless.io.

-------------------------------------
	  NLog Integration
-------------------------------------
You must add a Exceptionless NLog target and configure the logger rules to output to exceptionless.

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
