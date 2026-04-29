using Exceptionless.Core.Extensions;
using Exceptionless.Core.Validation;
using Exceptionless.Plugins;

namespace Exceptionless.Web.Utility.Handlers;

public class ExceptionToProblemDetailsHandler()
    : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var contextData = new ContextData();
        contextData.MarkAsUnhandledError();
        contextData.SetSubmissionMethod(nameof(ExceptionToProblemDetailsHandler));
        var builder = exception.ToExceptionless(contextData).SetHttpContext(httpContext);
        builder.Submit();

        string referenceId = builder.Target.ReferenceId;
        if (!String.IsNullOrEmpty(referenceId))
        {
            httpContext.Items.Add("reference-id", referenceId);
        }

        if (exception is MiniValidatorException validationException)
        {
            httpContext.Items.Add("errors", validationException.Errors.ToDictionary(
                error => error.Key.ToLowerUnderscoredWords(),
                error => error.Value
            ));
        }

        return ValueTask.FromResult(false);
    }
}
