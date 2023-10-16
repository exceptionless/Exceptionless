using System.IO.Pipelines;
using System.Security.Claims;
using Exceptionless.Core.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using MiniValidation;

namespace Exceptionless.Web.Utility;

public class AutoValidationActionFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;

    public AutoValidationActionFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context is { Controller: ControllerBase controllerBase, ActionDescriptor: ControllerActionDescriptor actionDescriptor })
        {
            var parametersToValidate = actionDescriptor.MethodInfo.GetParameters()
                .Where(p => ShouldValidate(p.ParameterType, _serviceProvider as IServiceProviderIsService))
                .ToArray();

            bool hasErrors = false;
            foreach (var parameter in parametersToValidate)
            {
                if (parameter.Name == null || !context.ActionArguments.TryGetValue(parameter.Name, out object? subject) || subject is null)
                    continue;

                // We don't support validating JSON Types
                if (subject is Newtonsoft.Json.Linq.JToken)
                    continue;

                (bool isValid, var errors) = await MiniValidator.TryValidateAsync(subject, _serviceProvider, recurse: true);
                if (isValid)
                    continue;

                foreach (var error in errors)
                {
                    // TODO: Verify nested object keys
                    string key = error.Key.ToLowerUnderscoredWords();
                    var modelStateEntry = context.ModelState[key];
                    foreach (string errorMessage in error.Value)
                    {
                        hasErrors = true;
                        if (modelStateEntry is null || !modelStateEntry.Errors.Contains(e => String.Equals(e.ErrorMessage, errorMessage)))
                            context.ModelState.AddModelError(key, errorMessage);
                    }
                }
            }

            if (hasErrors)
            {
                var validationProblem = controllerBase.ProblemDetailsFactory.CreateValidationProblemDetails(context.HttpContext, context.ModelState, 422);
                context.Result = new UnprocessableEntityObjectResult(validationProblem);

                return;
            }
        }

        await next();
    }

    private static bool ShouldValidate(Type type, IServiceProviderIsService? isService = null) =>
        !IsNonValidatedType(type, isService) && MiniValidator.RequiresValidation(type);

    private static bool IsNonValidatedType(Type type, IServiceProviderIsService? isService) =>
        typeof(HttpContext) == type
        || typeof(HttpRequest) == type
        || typeof(HttpResponse) == type
        || typeof(ClaimsPrincipal) == type
        || typeof(CancellationToken) == type
        || typeof(IFormFileCollection) == type
        || typeof(IFormFile) == type
        || typeof(Stream) == type
        || typeof(PipeReader) == type
        || isService?.IsService(type) == true;
}
