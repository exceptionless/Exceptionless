using Exceptionless.EmailTemplates.Models;

namespace Exceptionless.EmailTemplates;

public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync(EmailTemplate template, CancellationToken cancellationToken = default);
}
