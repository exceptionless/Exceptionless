using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Web.Models.Admin;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Controllers;

[Route(API_PREFIX + "/admin/oauth-applications")]
[Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
[ApiExplorerSettings(IgnoreApi = true)]
public class OAuthApplicationController(IOAuthApplicationRepository repository, TimeProvider timeProvider) : ExceptionlessApiController(timeProvider)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ViewOAuthApplication>>> GetAllAsync()
    {
        var results = await repository.FindAsync(q => q.SortAscending(a => a.Name), o => o.PageLimit(100));
        return Ok(results.Documents.Select(ViewOAuthApplication.FromApplication).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<ViewOAuthApplication>> CreateAsync(NewOAuthApplication model)
    {
        if (!await IsClientIdAvailableAsync(model.ClientId))
        {
            ModelState.AddModelError(nameof(model.ClientId), "Client id is already in use.");
            return ValidationProblem(ModelState);
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var application = new OAuthApplication
        {
            Id = ObjectId.GenerateNewId().ToString(),
            ClientId = model.ClientId.Trim(),
            Name = model.Name.Trim(),
            RedirectUris = NormalizeValues(model.RedirectUris, StringComparer.Ordinal),
            Scopes = NormalizeScopes(model.Scopes),
            Notes = model.Notes?.Trim(),
            IsDisabled = model.IsDisabled,
            CreatedByUserId = CurrentUser.Id,
            UpdatedByUserId = CurrentUser.Id,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        };

        await repository.AddAsync(application, o => o.ImmediateConsistency());
        return Created($"/api/v2/admin/oauth-applications/{application.Id}", ViewOAuthApplication.FromApplication(application));
    }

    [HttpPut("{id:objectid}")]
    public async Task<ActionResult<ViewOAuthApplication>> UpdateAsync(string id, UpdateOAuthApplication model)
    {
        var application = await repository.GetByIdAsync(id, o => o.ImmediateConsistency());
        if (application is null)
            return NotFound();

        if (!String.Equals(application.ClientId, model.ClientId, StringComparison.Ordinal) && !await IsClientIdAvailableAsync(model.ClientId))
        {
            ModelState.AddModelError(nameof(model.ClientId), "Client id is already in use.");
            return ValidationProblem(ModelState);
        }

        application.ClientId = model.ClientId.Trim();
        application.Name = model.Name.Trim();
        application.RedirectUris = NormalizeValues(model.RedirectUris, StringComparer.Ordinal);
        application.Scopes = NormalizeScopes(model.Scopes);
        application.Notes = model.Notes?.Trim();
        application.IsDisabled = model.IsDisabled;
        application.UpdatedByUserId = CurrentUser.Id;
        application.UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime;

        await repository.SaveAsync(application, o => o.ImmediateConsistency());
        return Ok(ViewOAuthApplication.FromApplication(application));
    }

    [HttpDelete("{id:objectid}")]
    public async Task<IActionResult> DeleteAsync(string id)
    {
        var application = await repository.GetByIdAsync(id, o => o.ImmediateConsistency());
        if (application is null)
            return NotFound();

        await repository.RemoveAsync(application, o => o.ImmediateConsistency());
        return NoContent();
    }

    private async Task<bool> IsClientIdAvailableAsync(string clientId)
    {
        var existing = await repository.GetByClientIdAsync(clientId.Trim(), o => o.ImmediateConsistency());
        return existing is null;
    }

    private static string[] NormalizeValues(IEnumerable<string> values, IEqualityComparer<string> comparer)
    {
        return values
            .Where(v => !String.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(comparer)
            .ToArray();
    }

    private static string[] NormalizeScopes(IEnumerable<string> scopes)
    {
        return scopes
            .Where(s => !String.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
