using Exceptionless.Core.Queries.Validation;
using Exceptionless.Web.Api.Messages;

namespace Exceptionless.Web.Api.Handlers;

public class UtilityHandler(
    PersistentEventQueryValidator eventQueryValidator,
    StackQueryValidator stackQueryValidator)
{
    public async Task<AppQueryValidator.QueryProcessResult> Handle(ValidateSearchQuery message)
    {
        try
        {
            var eventResults = await eventQueryValidator.ValidateQueryAsync(message.Query);
            var stackResults = await stackQueryValidator.ValidateQueryAsync(message.Query);
            return new AppQueryValidator.QueryProcessResult
            {
                IsValid = eventResults.IsValid || stackResults.IsValid,
                UsesPremiumFeatures = eventResults.UsesPremiumFeatures && stackResults.UsesPremiumFeatures,
                Message = eventResults.Message ?? stackResults.Message
            };
        }
        catch (FormatException)
        {
            return new AppQueryValidator.QueryProcessResult
            {
                IsValid = false,
                Message = $"Error parsing query: \"{message.Query}\""
            };
        }
        catch (ArgumentException)
        {
            return new AppQueryValidator.QueryProcessResult
            {
                IsValid = false,
                Message = $"Error parsing query: \"{message.Query}\""
            };
        }
    }
}
