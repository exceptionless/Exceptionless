using System.Text.Json;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Tests.Api;

public sealed class ValidationSnapshotTests : IntegrationTestsBase
{
    public ValidationSnapshotTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task PostAsync_EmptyProjectName_ReturnsProblemDetailsWithCamelCaseProperties()
    {
        // Act
        var response = await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("projects")
            .Content(new
            {
                organization_id = SampleDataService.TEST_ORG_ID,
                name = String.Empty
            })
            .StatusCodeShouldBeBadRequest()
        );

        using var document = await AssertProblemDetailsAsync(response, StatusCodes.Status400BadRequest);
        Assert.Equal("Project name is required.", document.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task PatchAsync_EmptyBody_ReturnsProblemDetailsWithCamelCaseProperties()
    {
        // Act
        var response = await SendRequestAsync(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID)
            .StatusCodeShouldBeBadRequest()
        );

        // Assert
        using var _ = await AssertProblemDetailsAsync(response, StatusCodes.Status400BadRequest, expectErrors: true);
    }

    [Fact]
    public async Task SetUserDescriptionAsync_EmptyPayload_ReturnsValidationProblemDetailsWithCamelCaseProperties()
    {
        // Act
        var response = await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("events/by-ref/TestReferenceId/user-description")
            .Content(new { })
            .StatusCodeShouldBeUnprocessableEntity()
        );

        using var document = await AssertProblemDetailsAsync(response, StatusCodes.Status422UnprocessableEntity, expectErrors: true);
        var errors = document.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("email_address", out _));
        Assert.True(errors.TryGetProperty("description", out _));
    }

    private static async Task<JsonDocument> AssertProblemDetailsAsync(HttpResponseMessage response, int statusCode, bool expectErrors = false)
    {
        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        Assert.True(
            mediaType is "application/problem+json" or "application/json",
            $"Expected application/problem+json or application/json but got '{mediaType}'");

        string content = await response.Content.ReadAsStringAsync();
        var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("type", out _));
        Assert.True(root.TryGetProperty("title", out _));
        Assert.True(root.TryGetProperty("status", out var status));
        Assert.Equal(statusCode, status.GetInt32());
        Assert.True(root.TryGetProperty("instance", out _));

        Assert.False(root.TryGetProperty("Status", out _));
        Assert.False(root.TryGetProperty("Title", out _));
        Assert.False(root.TryGetProperty("reference_id", out _));

        if (expectErrors)
            Assert.True(root.TryGetProperty("errors", out _));
        else
            Assert.False(root.TryGetProperty("errors", out _));

        return document;
    }
}
