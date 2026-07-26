using System.Security.Claims;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Web.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Exceptionless.Tests.Api.Infrastructure;

public sealed class ApiFilterPolicyTests
{
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void CombineStackModeQueryValidation_UsesUnionOfFreeFields(
        bool eventUsesPremiumFeatures,
        bool stackUsesPremiumFeatures,
        bool expectedUsesPremiumFeatures)
    {
        var eventValidation = new AppQueryValidator.QueryProcessResult { IsValid = true, UsesPremiumFeatures = eventUsesPremiumFeatures };
        var stackValidation = new AppQueryValidator.QueryProcessResult { IsValid = true, UsesPremiumFeatures = stackUsesPremiumFeatures };

        var result = ApiFilterPolicy.CombineStackModeQueryValidation(eventValidation, stackValidation);

        Assert.True(result.IsValid);
        Assert.Equal(expectedUsesPremiumFeatures, result.UsesPremiumFeatures);
    }

    [Theory]
    [InlineData(false, true, "Invalid event filter")]
    [InlineData(true, false, "Invalid stack filter")]
    public void CombineStackModeQueryValidation_InvalidFilter_ReturnsFailure(
        bool eventIsValid,
        bool stackIsValid,
        string expectedMessage)
    {
        var eventValidation = new AppQueryValidator.QueryProcessResult { IsValid = eventIsValid, Message = "Invalid event filter" };
        var stackValidation = new AppQueryValidator.QueryProcessResult { IsValid = stackIsValid, Message = "Invalid stack filter" };

        var result = ApiFilterPolicy.CombineStackModeQueryValidation(eventValidation, stackValidation);

        Assert.False(result.IsValid);
        Assert.Equal(expectedMessage, result.Message);
    }

    [Theory]
    [InlineData("organization:537650f3b77efe23a47914f3 tags:important")]
    [InlineData("project:537650f3b77efe23a47914f4 tags:important")]
    [InlineData("stack:537650f3b77efe23a47914f5 tags:important")]
    public void ShouldApplySystemFilter_GlobalAdminExplicitScope_ReturnsFalse(string userFilter)
    {
        var filter = new AppFilter([]) { IsUserOrganizationsFilter = true };
        var request = CreateGlobalAdminRequest();

        bool shouldApply = ApiFilterPolicy.ShouldApplySystemFilter(filter, userFilter, request);

        Assert.False(shouldApply);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tags:important")]
    public void ShouldApplySystemFilter_GlobalAdminWithoutExplicitScope_ReturnsTrue(string? userFilter)
    {
        var filter = new AppFilter([]) { IsUserOrganizationsFilter = true };
        var request = CreateGlobalAdminRequest();

        bool shouldApply = ApiFilterPolicy.ShouldApplySystemFilter(filter, userFilter, request);

        Assert.True(shouldApply);
    }

    [Fact]
    public void ShouldApplySystemFilter_ControllerScopedFilter_ReturnsTrue()
    {
        var filter = new AppFilter([]);
        var request = CreateGlobalAdminRequest();

        bool shouldApply = ApiFilterPolicy.ShouldApplySystemFilter(filter, "organization:537650f3b77efe23a47914f3", request);

        Assert.True(shouldApply);
    }

    [Fact]
    public void ShouldApplySystemFilter_NonAdminExplicitScope_ReturnsTrue()
    {
        var filter = new AppFilter([]) { IsUserOrganizationsFilter = true };
        var request = new DefaultHttpContext().Request;

        bool shouldApply = ApiFilterPolicy.ShouldApplySystemFilter(filter, "organization:537650f3b77efe23a47914f3", request);

        Assert.True(shouldApply);
    }

    [Fact]
    public void IsPremiumFeatureQueryBlocked_FreeOrganizationUsingPremiumFeatures_ReturnsTrue()
    {
        var filter = new AppFilter([new Organization { HasPremiumFeatures = false }])
        {
            UsesPremiumFeatures = true
        };

        bool isBlocked = ApiFilterPolicy.IsPremiumFeatureQueryBlocked(filter);

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

        bool isBlocked = ApiFilterPolicy.IsPremiumFeatureQueryBlocked(filter);

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

        bool isBlocked = ApiFilterPolicy.IsPremiumFeatureQueryBlocked(filter);

        Assert.False(isBlocked);
    }

    [Fact]
    public void IsPremiumFeatureQueryBlocked_EmptyOrganizationScope_ReturnsFalse()
    {
        var filter = new AppFilter([])
        {
            UsesPremiumFeatures = true
        };

        bool isBlocked = ApiFilterPolicy.IsPremiumFeatureQueryBlocked(filter);

        Assert.False(isBlocked);
    }

    private static HttpRequest CreateGlobalAdminRequest()
    {
        var context = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, AuthorizationRoles.GlobalAdmin)],
                authenticationType: "Test"))
        };

        return context.Request;
    }
}
