using Exceptionless.EmailTemplates.Models;
using Exceptionless.EmailTemplates.Templates;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace Exceptionless.EmailTemplates;

public sealed class RazorEmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;

    public RazorEmailTemplateRenderer(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _loggerFactory = loggerFactory;
    }

    public Task<string> RenderAsync(EmailTemplate template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        cancellationToken.ThrowIfCancellationRequested();

        return template switch
        {
            ContactRequestEmail value => RenderComponentAsync<ContactRequest>(value),
            EventNoticeEmail value => RenderComponentAsync<EventNotice>(value),
            OrganizationAddedEmail value => RenderComponentAsync<OrganizationAdded>(value),
            OrganizationInvitedEmail value => RenderComponentAsync<OrganizationInvited>(value),
            OrganizationNoticeEmail value => RenderComponentAsync<OrganizationNotice>(value),
            OrganizationPaymentFailedEmail value => RenderComponentAsync<OrganizationPaymentFailed>(value),
            ProjectDailySummaryEmail value => RenderComponentAsync<ProjectDailySummary>(value),
            UserEmailVerifyEmail value => RenderComponentAsync<UserEmailVerify>(value),
            UserPasswordResetEmail value => RenderComponentAsync<UserPasswordReset>(value),
            _ => throw new NotSupportedException($"No Razor component is registered for {template.GetType().Name}.")
        };
    }

    private async Task<string> RenderComponentAsync<TComponent>(EmailTemplate template) where TComponent : IComponent
    {
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            ["Model"] = template
        });
        await using var renderer = new HtmlRenderer(_serviceProvider, _loggerFactory);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>(parameters);
            return output.ToHtmlString();
        });
    }
}
