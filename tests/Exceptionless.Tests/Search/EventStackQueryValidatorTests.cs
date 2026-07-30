using Exceptionless.Core.Queries.Validation;
using Xunit;

namespace Exceptionless.Tests.Search;

public sealed class EventStackQueryValidatorTests : TestWithServices
{
    private readonly EventStackQueryValidator _validator;

    public EventStackQueryValidatorTests(ITestOutputHelper output) : base(output)
    {
        _validator = GetService<EventStackQueryValidator>();
    }

    [Theory]
    [InlineData("critical:false", false)]
    [InlineData("first:true", false)]
    [InlineData("reference:ABC123", false)]
    [InlineData("reference:ABC123 first:true", false)]
    [InlineData("reference:ABC123 first:true tags:important", true)]
    [InlineData("reference_id:ABC123", false)]
    [InlineData("stack:ABC123", false)]
    [InlineData("stack_id:ABC123", false)]
    [InlineData("tags:important", true)]
    public async Task ValidateQueryAsync_WithEventAndStackFields_ReturnsExpectedPremiumUsage(string query, bool usesPremiumFeatures)
    {
        // Arrange is provided by the theory data.

        // Act
        var result = await _validator.ValidateQueryAsync(query);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(usesPremiumFeatures, result.UsesPremiumFeatures);
    }
}
