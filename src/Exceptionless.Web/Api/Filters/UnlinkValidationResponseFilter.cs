using Exceptionless.Web.Models;

namespace Exceptionless.Web.Api.Filters;

public sealed class UnlinkValidationResponseFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);
        // Keep the existing unlink response while the request validator owns input validation.
        if (context.Arguments.Any(argument => argument is RemoveExternalLoginRequest)
            && result is IValueHttpResult { Value: HttpValidationProblemDetails problem }
            && problem.Errors.ContainsKey("provider_user_id"))
            return Microsoft.AspNetCore.Http.Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid Provider Name or Provider User Id.");

        return result;
    }
}
