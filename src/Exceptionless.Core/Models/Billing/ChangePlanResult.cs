namespace Exceptionless.Core.Models.Billing;

public record ChangePlanResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    public static ChangePlanResult FailWithMessage(string message)
    {
        return new ChangePlanResult { Message = message };
    }

    public static ChangePlanResult SuccessWithMessage(string message)
    {
        return new ChangePlanResult { Success = true, Message = message };
    }
}
