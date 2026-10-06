using Microsoft.Extensions.DependencyInjection;

namespace Exceptionless.EmailTemplates;

public static class EmailTemplateServiceCollectionExtensions
{
    public static IServiceCollection AddEmailTemplates(this IServiceCollection services)
    {
        services.AddSingleton<IEmailTemplateRenderer, RazorEmailTemplateRenderer>();
        return services;
    }
}
