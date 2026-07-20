using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services.SourceMaps;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Utility.OpenApi;
using Foundatio.Repositories;
using Microsoft.AspNetCore.Mvc;
using HttpIResult = Microsoft.AspNetCore.Http.IResult;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Exceptionless.Web.Api.Endpoints;

public static class SourceMapEndpoints
{
    public static IEndpointRouteBuilder MapSourceMapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        long maximumUploadRequestSize = GetMaximumUploadRequestSize(endpoints.ServiceProvider.GetRequiredService<AppOptions>().SourceMapOptions);
        var group = endpoints.MapGroup("api/v2")
            .WithTags("Source Map");

        group.MapGet("projects/{projectId:objectid}/source-maps", GetAsync)
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .Produces<IReadOnlyCollection<SourceMapArtifact>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get source maps for a project")
            .WithMetadata(new EndpointDocumentation {
                ParameterDescriptions = new() {
                    ["projectId"] = "The identifier of the project.",
                },
                ResponseDescriptions = new() {
                    ["404"] = "The project could not be found.",
                }
            });

        group.MapPost("projects/{projectId:objectid}/source-maps", PostAsync)
            .RequireAuthorization(AuthorizationRoles.SourceMapsWritePolicy)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<SourceMapArtifact>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithMetadata(
                new MultipartFileUploadAttribute("file", "generated_file_url") { FileDescription = "The source map file to upload." },
                new RequestSizeLimitAttribute(maximumUploadRequestSize),
                new RequestFormLimitsAttribute { MultipartBodyLengthLimit = maximumUploadRequestSize })
            .WithSummary("Upload a source map for a generated JavaScript file")
            .WithMetadata(new EndpointDocumentation {
                ParameterDescriptions = new() {
                    ["projectId"] = "The identifier of the project.",
                },
                ResponseDescriptions = new() {
                    ["201"] = "Created",
                    ["404"] = "The project could not be found.",
                    ["422"] = "The source map file or generated file URL is invalid.",
                }
            })
            .DisableAntiforgery();

        group.MapDelete("projects/{projectId:objectid}/source-maps/{sourceMapId}", DeleteAsync)
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete a source map from a project")
            .WithMetadata(new EndpointDocumentation {
                ParameterDescriptions = new() {
                    ["projectId"] = "The identifier of the project.",
                    ["sourceMapId"] = "The identifier of the source map.",
                },
                ResponseDescriptions = new() {
                    ["404"] = "The project or source map could not be found.",
                }
            });

        return endpoints;
    }

    internal static long GetMaximumUploadRequestSize(SourceMapOptions options)
        => (long)options.MaximumSourceMapSize + 1024 * 1024;

    private static async Task<HttpIResult> GetAsync(
        string projectId,
        HttpContext httpContext,
        IProjectRepository projectRepository,
        SourceMapService sourceMapService,
        CancellationToken cancellationToken)
    {
        if (await GetAccessibleProjectAsync(projectId, httpContext, projectRepository) is null)
            return HttpResults.NotFound();

        return HttpResults.Ok(await sourceMapService.GetArtifactsAsync(projectId, cancellationToken));
    }

    private static async Task<HttpIResult> PostAsync(
        string projectId,
        HttpContext httpContext,
        IProjectRepository projectRepository,
        IOrganizationRepository organizationRepository,
        BillingPlans billingPlans,
        SourceMapService sourceMapService,
        CancellationToken cancellationToken)
    {
        var project = await GetAccessibleProjectAsync(projectId, httpContext, projectRepository);
        if (project is null)
            return HttpResults.NotFound();

        var organization = await organizationRepository.GetByIdAsync(project.OrganizationId, options => options.Cache());
        if (organization is null)
            return HttpResults.NotFound();

        var (form, formError) = await ReadFormAsync(httpContext.Request, cancellationToken);
        if (formError is not null)
            return formError;

        var errors = new Dictionary<string, string[]>();
        string? generatedFileUrl = form!["generated_file_url"].FirstOrDefault();
        var file = form.Files.GetFile("file");

        if (String.IsNullOrWhiteSpace(generatedFileUrl))
            errors["generated_file_url"] = ["The generated file URL is required."];
        if (file is null || file.Length == 0)
            errors["file"] = ["A source map file is required."];
        if (errors.Count > 0)
            return HttpResults.ValidationProblem(errors, statusCode: StatusCodes.Status422UnprocessableEntity);

        try
        {
            await using var stream = file!.OpenReadStream();
            bool isFreePlan = String.Equals(organization.PlanId, billingPlans.FreePlan.Id, StringComparison.OrdinalIgnoreCase);
            var artifact = await sourceMapService.SaveUploadedAsync(projectId, generatedFileUrl!, file.FileName, stream, isFreePlan, cancellationToken);
            return HttpResults.Json(artifact, statusCode: StatusCodes.Status201Created);
        }
        catch (ArgumentException ex)
        {
            errors["generated_file_url"] = [ex.Message];
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            errors["file"] = [ex.Message];
        }

        return HttpResults.ValidationProblem(errors, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    internal static async Task<(IFormCollection? Form, HttpIResult? Error)> ReadFormAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return (null, HttpResults.ValidationProblem(
                new Dictionary<string, string[]> { ["file"] = ["The request must use multipart/form-data."] },
                statusCode: StatusCodes.Status422UnprocessableEntity));
        }

        try
        {
            return (await request.ReadFormAsync(cancellationToken), null);
        }
        catch (Exception ex) when (ex is InvalidDataException or BadHttpRequestException)
        {
            return (null, HttpResults.ValidationProblem(
                new Dictionary<string, string[]> { ["file"] = ["The multipart form data is invalid."] },
                statusCode: StatusCodes.Status422UnprocessableEntity));
        }
    }

    private static async Task<HttpIResult> DeleteAsync(
        string projectId,
        string sourceMapId,
        HttpContext httpContext,
        IProjectRepository projectRepository,
        SourceMapService sourceMapService,
        CancellationToken cancellationToken)
    {
        if (await GetAccessibleProjectAsync(projectId, httpContext, projectRepository) is null)
            return HttpResults.NotFound();

        return await sourceMapService.DeleteArtifactAsync(projectId, sourceMapId, cancellationToken)
            ? HttpResults.NoContent()
            : HttpResults.NotFound();
    }

    private static async Task<Project?> GetAccessibleProjectAsync(string projectId, HttpContext httpContext, IProjectRepository projectRepository)
    {
        var project = await projectRepository.GetByIdAsync(projectId, options => options.Cache());
        if (project is null || !httpContext.Request.CanAccessOrganization(project.OrganizationId))
            return null;

        string? tokenProjectId = httpContext.Request.GetProjectId();
        if (httpContext.User.IsInRole(AuthorizationRoles.SourceMapsWrite) && !httpContext.User.IsInRole(AuthorizationRoles.User))
            return String.Equals(tokenProjectId, projectId, StringComparison.Ordinal) ? project : null;

        return String.IsNullOrEmpty(tokenProjectId) || String.Equals(tokenProjectId, projectId, StringComparison.Ordinal) ? project : null;
    }
}
