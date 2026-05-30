# Add CSP, HSTS or HPKP headers to an ASP.NET Core app

This library allows you to add Content Security Policy, Strict Transport Security and Public Key Pin headers via middleware.

You can get the library from NuGet: [https://www.nuget.org/packages/Joonasw.AspNetCore.SecurityHeaders](https://www.nuget.org/packages/Joonasw.AspNetCore.SecurityHeaders)

## Example configuration

```cs
// Enable Strict Transport Security with a 30-day caching period
// Do not include subdomains
// Do not allow preload
app.UseStrictTransportSecurity(new HstsOptions(TimeSpan.FromDays(30), includeSubDomains: false, preload: false));

// Use certificate pinning with:
// - 30-day caching period
// - One pin in SHA-256 form
// - Report-Only = Invalid certificate should not be reported, but:
// - Report problems to /hpkp-report
app.UseHpkp(hpkp =>
{
    hpkp.UseMaxAgeSeconds(30 * 24 * 60 * 60)
        .AddSha256Pin("nrmpk4ZI3wbRBmUZIT5aKAgP0LlKHRgfA2Snjzeg9iY=")
        .SetReportOnly()
        .ReportViolationsTo("/hpkp-report");
});

// Content Security Policy
app.UseCsp(csp =>
{
    // If nothing is mentioned for a resource class, allow from this domain
    csp.ByDefaultAllow
        .FromSelf();

    // Allow JavaScript from:
    csp.AllowScripts
        .FromSelf() //This domain
        .From("localhost:1591") //These two domains
        .From("ajax.aspnetcdn.com");

    // CSS allowed from:
    csp.AllowStyles
        .FromSelf()
        .From("ajax.aspnetcdn.com");

    csp.AllowImages
        .FromSelf();

    // HTML5 audio and video elemented sources can be from:
    csp.AllowAudioAndVideo
        .FromNowhere();

    // Contained iframes can be sourced from:
    csp.AllowFrames
        .FromNowhere(); //Nowhere, no iframes allowed

    // Allow AJAX, WebSocket and EventSource connections to:
    csp.AllowConnections
        .To("ws://localhost:1591")
        .To("http://localhost:1591")
        .ToSelf();

    // Allow fonts to be downloaded from:
    csp.AllowFonts
        .FromSelf()
        .From("ajax.aspnetcdn.com");

    // Allow object, embed, and applet sources from:
    csp.AllowPlugins
        .FromNowhere();

    // Allow other sites to put this in an iframe?
    csp.AllowFraming
        .FromNowhere(); // Block framing on other sites, equivalent to X-Frame-Options: DENY

    // Do not block violations, only report
    // This is a good idea while testing your CSP
    // Remove it when you know everything will work
    csp.SetReportOnly();
    // Where should the violation reports be sent to?
    csp.ReportViolationsTo("/csp-report");

    // Do not include the CSP header for requests to the /api endpoints
    csp.OnSendingHeader = context =>
    {
        context.ShouldNotSend = context.HttpContext.Request.Path.StartsWithSegments("/api");
        return Task.CompletedTask;
    };
});
```

Content Security Policy can be quite daunting. Here is a nice page to find out what the options do: [https://content-security-policy.com/](https://content-security-policy.com/.)

For violation reports, I recommend using Scott Helme's Report URI service at [https://report-uri.io/](https://report-uri.io/).

## Nonces

CSP allows you to also specify a nonce value, which makes it easier to have inline script and style elements like this on a page:

```html
<head>
  <script>
    console.log("Hello");
  </script>
  <style>
    h1 {
      color: red;
    }
  </style>
</head>
```

To allow them without nonces, you might have to use the unsafe-inline option.

Instead of doing that, we can add the following service in `Startup`:

```cs
public void ConfigureServices(IServiceCollection services)
{
    // ... other service registrations

    // Add services necessary for nonces in CSP, 32-byte nonces
    services.AddCsp(nonceByteAmount: 32);
}
```

Then you need to modify your CSP definition to include the nonce:

```cs
csp.AllowScripts
    .FromSelf()
    .From("localhost:1591")
    .From("ajax.aspnetcdn.com")
    .AddNonce(); //<----

csp.AllowStyles
    .FromSelf()
    .From("ajax.aspnetcdn.com")
    .AddNonce(); //<-----
```

Then to use the nonce tag helper, we need to import it in *_ViewImports.cshtml*:

```c#
@addTagHelper *, Joonasw.AspNetCore.SecurityHeaders
```

Then we just need to use it in the Razor view:

```html
<head>
  <script asp-add-nonce="true">
    console.log("Hello");
  </script>
  <style asp-add-nonce="true">
    h1 {
      color: red;
    }
  </style>
</head>
```

Now a unique nonce is generated every request and inserted into the CSP header + the elements you want.
