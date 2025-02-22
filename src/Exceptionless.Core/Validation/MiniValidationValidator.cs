﻿using System.Text;
using MiniValidation;

namespace Exceptionless.Core.Validation;

public class MiniValidationValidator(IServiceProvider serviceProvider)
{
    public ValueTask<(bool IsValid, IDictionary<string, string[]> Errors)> ValidateAsync<T>(T instance)
    {
        return MiniValidator.TryValidateAsync(instance, serviceProvider, recurse: true);
    }

    public async Task ValidateAndThrowAsync<T>(T instance)
    {
        (bool isValid, var errors) = await ValidateAsync(instance);
        if (isValid)
            return;

        throw new MiniValidatorException("Please correct the specified errors and try again", errors);
    }
}

public class MiniValidatorException(string message, IDictionary<string, string[]> errors) : Exception(message)
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public string FormattedErrorMessages()
    {
        var errorMessages = new StringBuilder();
        errorMessages.AppendLine(Message);
        foreach (var error in Errors)
        {
            errorMessages.Append("- ").Append(error.Key).Append(": ");
            errorMessages.AppendJoin(", ", error.Value);
            errorMessages.AppendLine();
        }

        return errorMessages.ToString();
    }
}
