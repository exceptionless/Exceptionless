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
        httpContext.Items.Add("EventReferenceId", referenceId);

        return ValueTask.FromResult(false);
    }
}
