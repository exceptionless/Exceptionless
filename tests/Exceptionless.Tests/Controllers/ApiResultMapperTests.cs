using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Controllers;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class ApiResultMapperTests
{
    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status401Unauthorized)]
    [InlineData(StatusCodes.Status403Forbidden)]
    [InlineData(StatusCodes.Status404NotFound)]
    [InlineData(StatusCodes.Status409Conflict)]
    [InlineData(StatusCodes.Status500InternalServerError)]
    [InlineData(StatusCodes.Status503ServiceUnavailable)]
    public void MapNonSuccessResult_WithMappedFailure_ReturnsProblemDetailsWithExpectedStatusCode(int expectedStatusCode)
    {
        // Arrange
        var mediatorResult = Result.Error("Failure");

        // Act
        var result = expectedStatusCode switch
        {
            StatusCodes.Status400BadRequest => ApiResultMapper.MapBadRequest(mediatorResult),
            StatusCodes.Status401Unauthorized => ApiResultMapper.MapUnauthorized(mediatorResult),
            StatusCodes.Status403Forbidden => ApiResultMapper.MapForbidden(mediatorResult),
            StatusCodes.Status404NotFound => ApiResultMapper.MapNotFound(mediatorResult),
            StatusCodes.Status409Conflict => ApiResultMapper.MapConflict(mediatorResult),
            StatusCodes.Status500InternalServerError => ApiResultMapper.MapError(mediatorResult),
            StatusCodes.Status503ServiceUnavailable => ApiResultMapper.MapUnavailable(mediatorResult),
            _ => throw new ArgumentOutOfRangeException(nameof(expectedStatusCode))
        };

        // Assert
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(valueResult.Value);

        Assert.Equal(expectedStatusCode, statusCodeResult.StatusCode);
        Assert.Equal(expectedStatusCode, problemDetails.Status);
    }

    [Fact]
    public void MapResult_SuccessfulModelActionResults_ReturnsAcceptedWorkInProgressShape()
    {
        // Arrange
        var mapper = new ApiResultMapper();
        var modelActionResults = new ModelActionResults
        {
            Success = ["project-1"],
            Workers = ["worker-1"]
        };
        Result<ModelActionResults> mediatorResult = modelActionResults;

        // Act
        var result = mapper.MapResult(mediatorResult);

        // Assert
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var response = Assert.IsType<WorkInProgressResult>(valueResult.Value);

        Assert.Equal(StatusCodes.Status202Accepted, statusCodeResult.StatusCode);
        Assert.Equal(["worker-1"], response.Workers);
    }

    [Fact]
    public void MapValidation_CamelCaseAndDuplicateIdentifiers_ReturnsSnakeCaseDeduplicatedErrors()
    {
        // Arrange
        var mediatorResult = Result.Invalid(
            ValidationError.Create("organizationId", "Invalid organization."),
            ValidationError.Create("organization_id", "Invalid organization."),
            ValidationError.Create("organization_id", "Organization is unavailable."));

        // Act
        var result = ApiResultMapper.MapValidation(mediatorResult);

        // Assert
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(valueResult.Value);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, statusCodeResult.StatusCode);
        Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
        var organizationErrors = Assert.Single(problemDetails.Errors, error => error.Key == "organization_id").Value;
        Assert.Equal(2, organizationErrors.Length);
        Assert.Contains("Invalid organization.", organizationErrors);
        Assert.Contains("Organization is unavailable.", organizationErrors);
    }

    [Theory]
    [InlineData(ApiValidationErrorIdentifiers.PlanLimit, StatusCodes.Status426UpgradeRequired)]
    [InlineData(ApiValidationErrorIdentifiers.NotImplemented, StatusCodes.Status501NotImplemented)]
    [InlineData(ApiValidationErrorIdentifiers.RateLimit, StatusCodes.Status429TooManyRequests)]
    [InlineData(ApiValidationErrorIdentifiers.RequestEntityTooLarge, StatusCodes.Status413RequestEntityTooLarge)]
    public void MapValidation_WithSpecialIdentifier_ReturnsExpectedProblemDetailsStatusCode(string identifier, int expectedStatusCode)
    {
        // Arrange
        var mediatorResult = Result.Invalid(ValidationError.Create(identifier, "Failure"));

        // Act
        var result = ApiResultMapper.MapValidation(mediatorResult);

        // Assert
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(valueResult.Value);

        Assert.Equal(expectedStatusCode, statusCodeResult.StatusCode);
        Assert.Equal(expectedStatusCode, problemDetails.Status);
    }
}
