namespace Exceptionless.Web.Assistant;

public sealed class AssistantProviderException(string message) : Exception(message)
{
    internal string FailureCode { get; init; } = "provider_error";
}
