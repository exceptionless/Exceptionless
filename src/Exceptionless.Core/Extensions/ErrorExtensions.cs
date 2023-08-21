using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Extensions;

public static class ErrorExtensions
{

    public static void SetTargetInfo(this Error error, SettingsDictionary targetInfo)
    {
        error.Data ??= new DataDictionary();
        error.Data[Error.KnownDataKeys.TargetInfo] = targetInfo;
    }

    public static StackingTarget GetStackingTarget(this Error error)
    {
        if (error is null)
            throw new ArgumentNullException(nameof(error));

        InnerError? targetError = error;
        while (targetError is not null)
        {
            var frame = targetError.StackTrace?.FirstOrDefault(st => st.IsSignatureTarget.GetValueOrDefault());
            if (frame is not null)
                return new StackingTarget
                {
                    Error = targetError,
                    Method = frame
                };

            if (targetError.TargetMethod is not null && targetError.TargetMethod.IsSignatureTarget.GetValueOrDefault())
                return new StackingTarget
                {
                    Error = targetError,
                    Method = targetError.TargetMethod
                };

            targetError = targetError.Inner;
        }

        // fallback to default
        var defaultError = error.GetInnermostError();
        var defaultMethod = defaultError.StackTrace?.FirstOrDefault();
        if (defaultMethod is null && error.StackTrace is not null)
        {
            defaultMethod = error.StackTrace.FirstOrDefault();
            defaultError = error;
        }

        return new StackingTarget
        {
            Error = defaultError,
            Method = defaultMethod
        };
    }

    public static StackingTarget? GetStackingTarget(this Event ev)
    {
        var error = ev.GetError();
        return error?.GetStackingTarget();
    }

    public static InnerError GetInnermostError(this InnerError error)
    {
        if (error is null)
            throw new ArgumentNullException(nameof(error));

        var current = error;
        while (current.Inner is not null)
            current = current.Inner;

        return current;
    }
}

public record StackingTarget
{
    public required Method? Method { get; init; }
    public required InnerError Error { get; init; }
}
