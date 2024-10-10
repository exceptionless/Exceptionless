namespace Exceptionless.Web.Utility.Handlers;


public record ApiErrorItem
{
    public required string PropertyName { get; set; }
    public required string Message { get; set; }
    public object? AttemptedValue { get; set; }
}
