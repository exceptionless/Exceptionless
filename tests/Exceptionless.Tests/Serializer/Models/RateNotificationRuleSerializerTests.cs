using Exceptionless.Core.Models;
using Exceptionless.Core.Services;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

public class RateNotificationRuleSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public RateNotificationRuleSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Deserialize_MissingRuntimeFields_RemainsDisabledAndInvalid()
    {
        const string json = """
            {
              "id": "507f1f77bcf86cd799439011",
              "organization_id": "507f1f77bcf86cd799439012",
              "project_id": "507f1f77bcf86cd799439013",
              "user_id": "507f1f77bcf86cd799439014",
              "name": "Legacy incomplete rule"
            }
            """;

        var rule = _serializer.Deserialize<RateNotificationRule>(json);

        Assert.NotNull(rule);
        Assert.False(rule.IsEnabled);
        Assert.False(RateNotificationCounterPlan.IsValidRuntimeDefinition(rule, rule.ProjectId));
    }
}
