using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class StackStatusAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null)
            return true;

        return value is string status && Stack.KnownStatuses.All.Any(knownStatus =>
            String.Equals(knownStatus, status, StringComparison.OrdinalIgnoreCase));
    }

    public override string FormatErrorMessage(string name) =>
        $"{name} must be one of: {String.Join(", ", Stack.KnownStatuses.All)}.";
}
