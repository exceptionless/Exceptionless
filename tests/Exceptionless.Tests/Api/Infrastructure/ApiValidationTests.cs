using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Web.Api.Infrastructure;
using Xunit;

namespace Exceptionless.Tests.Api.Infrastructure;

public sealed class ApiValidationTests
{
    [Fact]
    public void IsPremiumFeatureQueryBlocked_FreeOrganizationUsingPremiumFeatures_ReturnsTrue()
    {
        var filter = new AppFilter([new Organization { HasPremiumFeatures = false }])
        {
            UsesPremiumFeatures = true
        };

        bool isBlocked = ApiValidation.IsPremiumFeatureQueryBlocked(filter);

        Assert.True(isBlocked);
    }

    [Fact]
    public void IsPremiumFeatureQueryBlocked_MixedOrganizationsUsingPremiumFeatures_ReturnsFalse()
    {
        var filter = new AppFilter([
            new Organization { HasPremiumFeatures = false },
            new Organization { HasPremiumFeatures = true }
        ])
        {
            UsesPremiumFeatures = true
        };

        bool isBlocked = ApiValidation.IsPremiumFeatureQueryBlocked(filter);

        Assert.False(isBlocked);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void IsPremiumFeatureQueryBlocked_NonBlockingScope_ReturnsFalse(bool usesPremiumFeatures, bool hasPremiumFeatures)
    {
        var filter = new AppFilter([new Organization { HasPremiumFeatures = hasPremiumFeatures }])
        {
            UsesPremiumFeatures = usesPremiumFeatures
        };

        bool isBlocked = ApiValidation.IsPremiumFeatureQueryBlocked(filter);

        Assert.False(isBlocked);
    }

    [Fact]
    public void IsPremiumFeatureQueryBlocked_EmptyOrganizationScope_ReturnsFalse()
    {
        var filter = new AppFilter([])
        {
            UsesPremiumFeatures = true
        };

        bool isBlocked = ApiValidation.IsPremiumFeatureQueryBlocked(filter);

        Assert.False(isBlocked);
    }
}
