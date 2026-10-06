namespace Exceptionless.Web.Assistant;

internal sealed class AssistantProviderCanceledException(string partialResponse, OperationCanceledException innerException)
    : OperationCanceledException("The AI provider response was cancelled.", innerException, innerException.CancellationToken)
{
    // Answer text is only for delivery to the browser, never exception diagnostics.
    internal string PartialResponse { get; } = partialResponse;
}
