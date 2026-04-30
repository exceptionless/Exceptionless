using System.Text;
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
        if (!isValid)
            throw new MiniValidatorException(errors);
    }
}

public class MiniValidatorException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public MiniValidatorException(IDictionary<string, string[]> errors)
        : base(FormatMessage(errors))
    {
        Errors = errors;
    }

    private static string FormatMessage(IDictionary<string, string[]> errors)
    {
        var sb = new StringBuilder("Please correct the specified errors and try again");
        foreach (var error in errors)
            sb.AppendLine().Append("- ").Append(error.Key).Append(": ").AppendJoin(", ", error.Value);
        return sb.ToString();
    }
}
