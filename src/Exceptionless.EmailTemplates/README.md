# Exceptionless email templates

This project is a Razor class library containing the transactional emails sent by Exceptionless. It is part of the existing .NET solution: there is no separate application, JavaScript toolchain, generated HTML, or runtime template compilation.

Each email has:

- a strongly typed model in `Models/EmailTemplate.cs`;
- a Razor component in `Templates/` that declares its model at the top with `@inherits EmailTemplateComponent<TModel>`;
- shared email-safe presentation components in `Components/`;
- automatic HTML encoding from Razor;
- rendering through the built-in ASP.NET Core `HtmlRenderer`.

`Exceptionless.Core.Mail.Mailer` creates the models and queues the rendered result. Template registration and dispatch are centralized in `RazorEmailTemplateRenderer`, whose generic constraints verify each component-to-model pairing at compile time.

Run the focused rendering and mailer tests from the repository root:

```powershell
dotnet test -- --filter-class Exceptionless.Tests.Mail.MailerTests
```

Generate a static gallery containing representative data and every conditional layout:

```powershell
dotnet run --no-cache --file build/EmailTemplatePreviews.cs
```

Open `artifacts/email-previews/index.html` in a browser. The gallery and individual rendered HTML files are ignored by Git.

To exercise email delivery and inspect the exact MIME message in the local Mailpit client, start the Aspire AppHost and run:

```powershell
dotnet run --no-cache --file build/EmailTemplatePreviews.cs -- --send
```

Mailpit is available at `http://localhost:8026` by default. Use `--smtp-host`, `--smtp-port`, and `--send-to` to override its connection details.

Critical presentation styles are authored inline by the shared Razor components. `EmailStyles.ClientCss` contains only client resets and responsive media queries, which cannot be expressed as inline declarations. There is no separate CSS-inlining build step.
