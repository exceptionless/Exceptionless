using Exceptionless.Core.Queries.Validation;
using Exceptionless.Web.Api.Messages;

namespace Exceptionless.Web.Api.Handlers;

public class UtilityHandler
{
    private readonly Func<string, Task<AppQueryValidator.QueryProcessResult>> _validateEventQueryAsync;
    private readonly Func<string, Task<AppQueryValidator.QueryProcessResult>> _validateStackQueryAsync;

    public UtilityHandler(
        PersistentEventQueryValidator eventQueryValidator,
        StackQueryValidator stackQueryValidator)
        : this(eventQueryValidator.ValidateQueryAsync, stackQueryValidator.ValidateQueryAsync) { }

    internal UtilityHandler(
        Func<string, Task<AppQueryValidator.QueryProcessResult>> validateEventQueryAsync,
        Func<string, Task<AppQueryValidator.QueryProcessResult>> validateStackQueryAsync)
    {
        _validateEventQueryAsync = validateEventQueryAsync;
        _validateStackQueryAsync = validateStackQueryAsync;
    }

    public async Task<AppQueryValidator.QueryProcessResult> Handle(ValidateSearchQuery message)
    {
        try
        {
            var eventResults = await _validateEventQueryAsync(message.Query);
            var stackResults = await _validateStackQueryAsync(message.Query);
            return new AppQueryValidator.QueryProcessResult
            {
                IsValid = eventResults.IsValid || stackResults.IsValid,
                UsesPremiumFeatures = eventResults.UsesPremiumFeatures && stackResults.UsesPremiumFeatures,
                Message = eventResults.Message ?? stackResults.Message
            };
        }
        catch (Exception)
        {
            return new AppQueryValidator.QueryProcessResult
            {
                IsValid = false,
                Message = $"Error parsing query: \"{message.Query}\""
            };
        }
    }
}
