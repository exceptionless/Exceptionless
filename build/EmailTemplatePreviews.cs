#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:project ../src/Exceptionless.EmailTemplates/Exceptionless.EmailTemplates.csproj

// Run from the repository root.
//
// Render the static preview gallery to artifacts/email-previews:
//   dotnet run --no-cache --file build/EmailTemplatePreviews.cs
//
// Render the gallery and send every preview to the Aspire-hosted Mailpit instance:
//   dotnet run --no-cache --file build/EmailTemplatePreviews.cs -- --send
//
// Options:
//   --output <directory>  Output directory (default: artifacts/email-previews)
//   --send                Send rendered previews over SMTP
//   --smtp-host <host>    SMTP host (default: localhost)
//   --smtp-port <port>    SMTP port (default: 1026)
//   --send-to <address>   Recipient address (default: preview@exceptionless.test)
//
// Start the Exceptionless AppHost before using --send. Mailpit is available at
// http://localhost:8026 by default.

using System.Net;
using System.Net.Mail;
using System.Text;
using Exceptionless.EmailTemplates;
using Exceptionless.EmailTemplates.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

string outputDirectory = GetOption(args, "--output") ?? Path.Combine("artifacts", "email-previews");
bool sendToMailpit = args.Contains("--send", StringComparer.OrdinalIgnoreCase);
string smtpHost = GetOption(args, "--smtp-host") ?? "localhost";
int smtpPort = Int32.TryParse(GetOption(args, "--smtp-port"), out int configuredPort) ? configuredPort : 1026;
string recipient = GetOption(args, "--send-to") ?? "preview@exceptionless.test";

var builder = Host.CreateApplicationBuilder();
builder.Services.AddEmailTemplates();
using IHost host = builder.Build();
var renderer = host.Services.GetRequiredService<IEmailTemplateRenderer>();

Directory.CreateDirectory(outputDirectory);
var renderedPreviews = new List<RenderedPreview>();

foreach (var preview in GetPreviews())
{
    string html = await renderer.RenderAsync(preview.Template);
    string fileName = $"{preview.Slug}.html";
    await File.WriteAllTextAsync(Path.Combine(outputDirectory, fileName), html);
    renderedPreviews.Add(new RenderedPreview(preview.Name, fileName, html));
}

await File.WriteAllTextAsync(Path.Combine(outputDirectory, "index.html"), BuildIndex(renderedPreviews));

if (sendToMailpit)
{
    using var client = new SmtpClient(smtpHost, smtpPort);
    foreach (var preview in renderedPreviews)
    {
        using var message = new MailMessage
        {
            From = new MailAddress("preview@exceptionless.test", "Exceptionless Email Preview"),
            Subject = $"[Preview] {preview.Name}",
            Body = preview.Html,
            IsBodyHtml = true
        };
        message.To.Add(recipient);
        await client.SendMailAsync(message);
    }

    Console.WriteLine($"Sent {renderedPreviews.Count} previews to {recipient} through {smtpHost}:{smtpPort}.");
}

Console.WriteLine($"Rendered {renderedPreviews.Count} previews to {Path.GetFullPath(outputDirectory)}.");

static string? GetOption(string[] arguments, string name)
{
    int index = Array.FindIndex(arguments, argument => String.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}

static string BuildIndex(IReadOnlyCollection<RenderedPreview> previews)
{
    var html = new StringBuilder("""
        <!doctype html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Exceptionless email previews</title>
            <style>
                body{background:#ececec;color:#2c2c2c;font:16px/1.5 system-ui,sans-serif;margin:0;padding:32px}
                main{margin:0 auto;max-width:1200px}h1{font-weight:500;margin-top:0}
                .grid{display:grid;gap:24px;grid-template-columns:repeat(auto-fit,minmax(360px,1fr))}
                article{background:#fff;border:1px solid #d0d0d0;border-radius:8px;box-shadow:0 2px 8px #0001;overflow:hidden}
                h2{font-size:16px;margin:0;padding:12px 16px}iframe{border:0;border-top:1px solid #ddd;height:680px;width:100%}
            </style>
        </head>
        <body><main><h1>Exceptionless email previews</h1><div class="grid">
        """);

    foreach (var preview in previews)
    {
        html.Append("<article><h2>")
            .Append(WebUtility.HtmlEncode(preview.Name))
            .Append("</h2><iframe title=\"")
            .Append(WebUtility.HtmlEncode(preview.Name))
            .Append("\" src=\"")
            .Append(WebUtility.HtmlEncode(preview.FileName))
            .Append("\"></iframe></article>");
    }

    return html.Append("</div></main></body></html>").ToString();
}

static IReadOnlyCollection<EmailPreview> GetPreviews()
{
    const string appUrl = "https://app.exceptionless.test";
    EmailLink[] GetActionLinks(string stackId) =>
    [
        new EmailLink("Mark event as fixed", $"{appUrl}/stack/{stackId}/mark-fixed"),
        new EmailLink("Stop sending notifications for this event", $"{appUrl}/stack/{stackId}/ignored"),
        new EmailLink("Discard future event occurrences", $"{appUrl}/stack/{stackId}/discarded"),
        new EmailLink("Change your notification settings for this project", $"{appUrl}/account/manage?projectId=project-1&tab=notifications")
    ];
    var stacks = new[]
    {
        new StackSummary("The operation timed out while processing the checkout request", "System.TimeoutException", true, $"{appUrl}/stack/stack-1"),
        new StackSummary("Object reference not set to an instance of an object", "System.NullReferenceException", false, $"{appUrl}/stack/stack-2"),
        new StackSummary("Unable to connect to the configured database server", "System.Data.DataException", false, $"{appUrl}/stack/stack-3")
    };

    return
    [
        new("contact-request", "Contact request", new ContactRequestEmail(
            "[Contact] Enterprise evaluation",
            "Ada Lovelace",
            "ada@example.com",
            "Analytical Engines, Inc.",
            "Enterprise evaluation",
            ["We would like to evaluate Exceptionless for our engineering organization.", "Please contact me about deployment options."],
            "203.0.113.42",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)",
            "https://exceptionless.com/contact")),
        new("event-new-critical", "New critical event", new EventNoticeEmail(
            "System.TimeoutException: The operation timed out",
            "Storefront API",
            true,
            true,
            false,
            1,
            new Dictionary<string, string>
            {
                ["Message"] = "The operation timed out while processing the checkout request.",
                ["Type"] = "System.TimeoutException",
                ["URL"] = "https://store.example.com/checkout/orders/123456789",
                ["Version"] = "8.2.1",
                ["Tags"] = "critical, checkout, production"
            },
            new EventUser("Ada Lovelace (ada@example.com)", "mailto:ada%40example.com?body=Checkout%20failed", "Checkout failed after submitting payment."),
            GetActionLinks("stack-1"),
            new EmailAction("View Event Details", $"{appUrl}/event/event-1"))),
        new("event-regression", "Regressed event", new EventNoticeEmail(
            "System.InvalidOperationException: Sequence contains no elements",
            "Storefront API",
            false,
            false,
            true,
            42,
            new Dictionary<string, string> { ["Message"] = "Sequence contains no elements", ["Version"] = "8.2.1" },
            null,
            GetActionLinks("stack-2"),
            new EmailAction("View Event Details", $"{appUrl}/event/event-2"))),
        new("organization-added", "Added to organization", new OrganizationAddedEmail(
            "Grace Hopper added you to the organization \"Engineering\" on Exceptionless",
            new EmailAction("View Organization", $"{appUrl}/organization/organization-1/dashboard"))),
        new("organization-invited", "Organization invitation", new OrganizationInvitedEmail(
            "Grace Hopper invited you to join the organization \"Engineering\" on Exceptionless",
            new EmailAction("Join Organization", $"{appUrl}/signup?token=preview-token"))),
        new("organization-monthly-limit", "Monthly plan limit", new OrganizationNoticeEmail(
            "[Engineering] Monthly plan limit exceeded.",
            "Engineering",
            true,
            false,
            "11:00 PM",
            $"{appUrl}/organization/organization-1/upgrade",
            $"{appUrl}/organization/organization-1/frequent",
            "https://github.com/exceptionless/Exceptionless/wiki/Frequently-Asked-Questions#q-what-happens-if-the-organization-plan-limit-is-reached",
            [new("View usage", $"{appUrl}/organization/organization-1/manage"), new("Change your notification settings", $"{appUrl}/account/manage?tab=notifications")])),
        new("organization-hourly-limit", "Hourly throttling", new OrganizationNoticeEmail(
            "[Engineering] Events are currently being throttled.",
            "Engineering",
            false,
            true,
            "11:00 PM",
            $"{appUrl}/organization/organization-1/upgrade",
            $"{appUrl}/organization/organization-1/frequent",
            "https://github.com/exceptionless/Exceptionless/wiki/Frequently-Asked-Questions#q-why-is-my-organization-throttled",
            [new("View usage", $"{appUrl}/organization/organization-1/manage"), new("Change your notification settings", $"{appUrl}/account/manage?tab=notifications")])),
        new("organization-payment-failed", "Payment failed", new OrganizationPaymentFailedEmail(
            "[Engineering] Payment failed! Update billing information to avoid service interruption!",
            "Engineering",
            $"{appUrl}/organization/organization-1/manage?tab=billing")),
        new("project-daily-summary", "Daily summary", new ProjectDailySummaryEmail(
            "[Storefront API] Summary for August 24, 2026",
            "Storefront API",
            "August 24, 2026",
            true,
            1284,
            37,
            8,
            3,
            0,
            false,
            stacks,
            stacks[..2],
            $"{appUrl}/project/project-1/error/timeline",
            $"{appUrl}/project/project-1/configure",
            $"{appUrl}/organization/organization-1/upgrade",
            $"{appUrl}/project/project-1/error/frequent",
            $"{appUrl}/project/project-1/error/new",
            $"{appUrl}/account/manage?projectId=project-1&tab=notifications")),
        new("project-daily-summary-throttled", "Daily summary with throttling", new ProjectDailySummaryEmail(
            "[Storefront API] Summary for August 24, 2026",
            "Storefront API",
            "August 24, 2026",
            true,
            98765,
            312,
            48,
            9,
            12500,
            true,
            stacks,
            stacks[..2],
            $"{appUrl}/project/project-1/error/timeline",
            $"{appUrl}/project/project-1/configure",
            $"{appUrl}/organization/organization-1/upgrade",
            $"{appUrl}/project/project-1/error/frequent",
            $"{appUrl}/project/project-1/error/new",
            $"{appUrl}/account/manage?projectId=project-1&tab=notifications")),
        new("project-not-configured", "Project not configured", new ProjectDailySummaryEmail(
            "[Storefront API] Summary for August 24, 2026",
            "Storefront API",
            "August 24, 2026",
            false,
            0,
            0,
            0,
            0,
            0,
            false,
            [],
            [],
            $"{appUrl}/project/project-1/error/timeline",
            $"{appUrl}/project/project-1/configure",
            $"{appUrl}/organization/organization-1/upgrade",
            $"{appUrl}/project/project-1/error/frequent",
            $"{appUrl}/project/project-1/error/new",
            $"{appUrl}/account/manage?projectId=project-1&tab=notifications")),
        new("user-email-verify", "Email verification", new UserEmailVerifyEmail(
            "Exceptionless Account Confirmation",
            "Ada Lovelace",
            new EmailAction("Verify Address", $"{appUrl}/account/verify?token=preview-token"))),
        new("user-password-reset", "Password reset", new UserPasswordResetEmail(
            "Exceptionless Password Reset",
            "Ada Lovelace",
            $"{appUrl}/reset-password/preview-token?cancel=true",
            new EmailAction("Reset Password", $"{appUrl}/reset-password/preview-token")))
    ];
}

internal sealed record EmailPreview(string Slug, string Name, EmailTemplate Template);

internal sealed record RenderedPreview(string Name, string FileName, string Html);
