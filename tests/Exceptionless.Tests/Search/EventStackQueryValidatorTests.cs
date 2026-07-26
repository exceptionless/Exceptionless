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
    [InlineData("reference:ABC123", false)]
    [InlineData("reference_id:ABC123", false)]
    [InlineData("stack:ABC123", false)]
    [InlineData("stack_id:ABC123", false)]
    [InlineData("first:true", false)]
    [InlineData("critical:false", false)]
    [InlineData("reference:ABC123 first:true", false)]
    [InlineData("tags:important", true)]
    [InlineData("reference:ABC123 first:true tags:important", true)]
    public async Task ValidateQueryAsync_UsesFreeFieldUnion(string query, bool usesPremiumFeatures)
    {
        var result = await _validator.ValidateQueryAsync(query);

        Assert.True(result.IsValid);
        Assert.Equal(usesPremiumFeatures, result.UsesPremiumFeatures);
    }
}
