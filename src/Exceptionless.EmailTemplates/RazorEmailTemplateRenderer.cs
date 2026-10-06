using Exceptionless.EmailTemplates.Components;
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
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _serviceProvider = serviceProvider;
        _loggerFactory = loggerFactory;
    }

    public Task<string> RenderAsync(EmailTemplate template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        cancellationToken.ThrowIfCancellationRequested();

        return template switch
        {
            ContactRequestEmail value => RenderComponentAsync<ContactRequest, ContactRequestEmail>(value),
            EventNoticeEmail value => RenderComponentAsync<EventNotice, EventNoticeEmail>(value),
            OrganizationAddedEmail value => RenderComponentAsync<OrganizationAdded, OrganizationAddedEmail>(value),
            OrganizationInvitedEmail value => RenderComponentAsync<OrganizationInvited, OrganizationInvitedEmail>(value),
            OrganizationNoticeEmail value => RenderComponentAsync<OrganizationNotice, OrganizationNoticeEmail>(value),
            OrganizationPaymentFailedEmail value => RenderComponentAsync<OrganizationPaymentFailed, OrganizationPaymentFailedEmail>(value),
            ProjectDailySummaryEmail value => RenderComponentAsync<ProjectDailySummary, ProjectDailySummaryEmail>(value),
            UserEmailVerifyEmail value => RenderComponentAsync<UserEmailVerify, UserEmailVerifyEmail>(value),
            UserPasswordResetEmail value => RenderComponentAsync<UserPasswordReset, UserPasswordResetEmail>(value),
            _ => throw new NotSupportedException($"No Razor component is registered for {template.GetType().Name}.")
        };
    }

    private async Task<string> RenderComponentAsync<TComponent, TModel>(TModel model)
        where TComponent : EmailTemplateComponent<TModel>
        where TModel : EmailTemplate
    {
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(EmailTemplateComponent<TModel>.Model)] = model
        });
        await using var renderer = new HtmlRenderer(_serviceProvider, _loggerFactory);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>(parameters);
            return output.ToHtmlString();
        });
    }
}
