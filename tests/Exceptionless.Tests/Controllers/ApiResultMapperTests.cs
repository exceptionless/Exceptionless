using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Controllers;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class ApiResultMapperTests
{
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
}
