using Exceptionless.Core.Queries.Validation;
using Exceptionless.Web.Api.Handlers;
using Exceptionless.Web.Api.Messages;
using Xunit;

namespace Exceptionless.Tests.Api.Handlers;

public sealed class UtilityHandlerTests
{
    [Theory]
    [MemberData(nameof(ValidatorExceptions))]
    public async Task Handle_ValidatorException_ReturnsInvalidSearchResult(bool throwFromEventValidator, Exception exception)
    {
        // Arrange
        const string query = "type:error";
        var validResult = new AppQueryValidator.QueryProcessResult { IsValid = true };
        Task<AppQueryValidator.QueryProcessResult> Throw() => Task.FromException<AppQueryValidator.QueryProcessResult>(exception);
        Task<AppQueryValidator.QueryProcessResult> Succeed() => Task.FromResult(validResult);
        var handler = new UtilityHandler(
            _ => throwFromEventValidator ? Throw() : Succeed(),
            _ => throwFromEventValidator ? Succeed() : Throw());

        // Act
        var result = await handler.Handle(new ValidateSearchQuery(query));

        // Assert
        Assert.False(result.IsValid);
        Assert.False(result.UsesPremiumFeatures);
        Assert.Equal($"Error parsing query: \"{query}\"", result.Message);
    }

    public static TheoryData<bool, Exception> ValidatorExceptions => new()
    {
        { true, new Exception("Event validation failed.") },
        { false, new Exception("Stack validation failed.") },
        { true, new OperationCanceledException("Event validation was canceled.") },
        { false, new OperationCanceledException("Stack validation was canceled.") }
    };
}
