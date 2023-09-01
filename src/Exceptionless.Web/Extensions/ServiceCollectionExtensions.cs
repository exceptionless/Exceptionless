using Exceptionless.Web.Utility;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Exceptionless.Web.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAutoValidation(this IServiceCollection serviceCollection)
    {
        // Create a default instance of the `ModelStateInvalidFilter` to access the non static property `Order` in a static context.
        var modelStateInvalidFilter = new ModelStateInvalidFilter(
            new ApiBehaviorOptions { InvalidModelStateResponseFactory = context => new OkResult() }, NullLogger.Instance);

        // Make sure we insert the `AutoValidationActionFilter` before the built-in `ModelStateInvalidFilter` to prevent it short-circuiting the request.
        serviceCollection.Configure<MvcOptions>(options =>
            options.Filters.Add<AutoValidationActionFilter>(modelStateInvalidFilter.Order - 1));

        return serviceCollection;
    }
}
