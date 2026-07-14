using System.Text.Json;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services.SourceMaps;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Utility.OpenApi;
using Foundatio.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Controllers;

[Route(API_PREFIX + "/projects/{projectId:objectid}/source-maps")]
public sealed class SourceMapController : ExceptionlessApiController
{
    private readonly IProjectRepository _projectRepository;
    private readonly SourceMapService _sourceMapService;

    public SourceMapController(IProjectRepository projectRepository, SourceMapService sourceMapService, TimeProvider timeProvider)
        : base(timeProvider)
    {
        _projectRepository = projectRepository;
        _sourceMapService = sourceMapService;
    }

    /// <summary>
    /// Get source maps for a project.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    public async Task<ActionResult<IReadOnlyCollection<SourceMapArtifact>>> GetAsync(string projectId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessProjectAsync(projectId))
            return NotFound();

        return Ok(await _sourceMapService.GetArtifactsAsync(projectId, cancellationToken));
    }

    /// <summary>
    /// Upload a source map for a generated JavaScript file.
    /// </summary>
    /// <param name="projectId">The identifier of the project.</param>
    /// <param name="generatedFileUrl">The exact absolute URL of the generated JavaScript file.</param>
    /// <param name="file">The source map file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [HttpPost]
    [Authorize(Policy = AuthorizationRoles.SourceMapsWritePolicy)]
    [Consumes("multipart/form-data")]
    [MultipartFileUpload("file", "generated_file_url", FileDescription = "The source map file to upload.")]
    [RequestSizeLimit(SourceMapService.MaximumUploadRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = SourceMapService.MaximumUploadRequestSize)]
    [ProducesResponseType<SourceMapArtifact>(StatusCodes.Status201Created)]
    public async Task<ActionResult<SourceMapArtifact>> PostAsync(
        string projectId,
        [FromForm(Name = "generated_file_url")] string? generatedFileUrl,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (!await CanAccessProjectAsync(projectId))
            return NotFound();

        if (String.IsNullOrWhiteSpace(generatedFileUrl))
            ModelState.AddModelError("generated_file_url", "The generated file URL is required.");
        if (file is null || file.Length == 0)
            ModelState.AddModelError("file", "A source map file is required.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            await using var stream = file!.OpenReadStream();
            var artifact = await _sourceMapService.SaveUploadedAsync(projectId, generatedFileUrl!, file.FileName, stream, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, artifact);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError("generated_file_url", ex.Message);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            ModelState.AddModelError("file", ex.Message);
        }

        return ValidationProblem(ModelState);
    }

    /// <summary>
    /// Delete a source map from a project.
    /// </summary>
    [HttpDelete("{sourceMapId}")]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(string projectId, string sourceMapId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessProjectAsync(projectId))
            return NotFound();

        return await _sourceMapService.DeleteArtifactAsync(projectId, sourceMapId, cancellationToken) ? NoContent() : NotFound();
    }

    private async Task<bool> CanAccessProjectAsync(string projectId)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, options => options.Cache());
        if (project is null || !CanAccessOrganization(project.OrganizationId))
            return false;

        string? tokenProjectId = Request.GetProjectId();
        if (User.IsInRole(AuthorizationRoles.SourceMapsWrite) && !User.IsInRole(AuthorizationRoles.User))
            return String.Equals(tokenProjectId, projectId, StringComparison.Ordinal);

        return String.IsNullOrEmpty(tokenProjectId) || String.Equals(tokenProjectId, projectId, StringComparison.Ordinal);
    }
}
