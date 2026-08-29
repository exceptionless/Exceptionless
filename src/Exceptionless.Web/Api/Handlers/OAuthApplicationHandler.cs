using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Validation;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models.Admin;
using Foundatio.Mediator;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;

namespace Exceptionless.Web.Api.Handlers;

public class OAuthApplicationHandler(
    IOAuthApplicationRepository repository,
    IOrganizationRepository organizationRepository,
    OAuthService oauthService,
    TimeProvider timeProvider)
{
    public async Task<Result<PagedResult<ViewOAuthApplication>>> Handle(GetOAuthApplications message)
    {
        int page = Pagination.GetPage(message.Page);
        int limit = Pagination.GetLimit(message.Limit);
        var organizationIds = await ResolveOrganizationIdsAsync(message.Organization);
        if (!String.IsNullOrWhiteSpace(message.Organization) && organizationIds.Count == 0)
            return new PagedResult<ViewOAuthApplication>([], false, page, 0);

        var results = await repository.GetByCriteriaAsync(message.Criteria, organizationIds, o => o.PageNumber(page).PageLimit(limit));
        var applications = await MapApplicationsAsync(results.Documents);
        return new PagedResult<ViewOAuthApplication>(applications, results.HasMore && !Pagination.NextPageExceedsSkipLimit(page, limit), page, results.Total);
    }

    public async Task<Result<ViewOAuthApplication>> Handle(GetOAuthApplication message)
    {
        var application = await repository.GetByIdAsync(message.Id);
        if (application is null)
            return Result.NotFound("OAuth application not found.");

        return (await MapApplicationsAsync([application])).Single();
    }

    public async Task<Result<ViewOAuthApplication>> Handle(CreateOAuthApplicationMessage message)
    {
        var model = message.Model;
        if (!await IsClientIdAvailableAsync(model.ClientId))
            return Result<ViewOAuthApplication>.FromResult(Result.Invalid(ValidationError.Create("client_id", "Client id is already in use.")));

        var currentUser = message.Context.Request.GetUser();
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var application = new OAuthApplication
        {
            Id = ObjectId.GenerateNewId().ToString(),
            ClientId = model.ClientId.Trim(),
            Name = model.Name.Trim(),
            RedirectUris = NormalizeValues(model.RedirectUris, StringComparer.Ordinal),
            Scopes = NormalizeScopes(model.Scopes),
            Notes = model.Notes?.Trim(),
            IsDisabled = model.IsDisabled,
            CreatedByUserId = currentUser.Id,
            UpdatedByUserId = currentUser.Id,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        };

        try
        {
            await repository.AddAsync(application, o => o.ImmediateConsistency());
        }
        catch (MiniValidatorException ex)
        {
            return ValidationResult<ViewOAuthApplication>(ex);
        }

        await oauthService.ClearAccessTokenClientValidityCacheAsync(application.ClientId);
        return Result<ViewOAuthApplication>.Created(ViewOAuthApplication.FromApplication(application), $"/api/v2/admin/oauth-applications/{application.Id}");
    }

    public async Task<Result<ViewOAuthApplication>> Handle(UpdateOAuthApplicationMessage message)
    {
        var model = message.Model;
        var application = await repository.GetByIdAsync(message.Id, o => o.ImmediateConsistency());
        if (application is null)
            return Result.NotFound("OAuth application not found.");

        string previousClientId = application.ClientId;
        if (!String.Equals(application.ClientId, model.ClientId, StringComparison.Ordinal) && !await IsClientIdAvailableAsync(model.ClientId))
            return Result<ViewOAuthApplication>.FromResult(Result.Invalid(ValidationError.Create("client_id", "Client id is already in use.")));

        application.ClientId = model.ClientId.Trim();
        application.Name = model.Name.Trim();
        application.RedirectUris = NormalizeValues(model.RedirectUris, StringComparer.Ordinal);
        application.Scopes = NormalizeScopes(model.Scopes);
        application.Notes = model.Notes?.Trim();
        application.IsDisabled = model.IsDisabled;
        application.UpdatedByUserId = message.Context.Request.GetUser().Id;
        application.UpdatedUtc = timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            await repository.SaveAsync(application, o => o.ImmediateConsistency());
        }
        catch (MiniValidatorException ex)
        {
            return ValidationResult<ViewOAuthApplication>(ex);
        }

        await oauthService.ClearAccessTokenClientValidityCacheAsync(previousClientId);
        await oauthService.ClearAccessTokenClientValidityCacheAsync(application.ClientId);
        return ViewOAuthApplication.FromApplication(application);
    }

    public async Task<Result> Handle(DeleteOAuthApplicationMessage message)
    {
        var application = await repository.GetByIdAsync(message.Id, o => o.ImmediateConsistency());
        if (application is null)
            return Result.NotFound("OAuth application not found.");

        await repository.RemoveAsync(application, o => o.ImmediateConsistency());
        await oauthService.ClearAccessTokenClientValidityCacheAsync(application.ClientId);
        return Result.NoContent();
    }

    private async Task<bool> IsClientIdAvailableAsync(string clientId)
    {
        var existing = await repository.GetByClientIdAsync(clientId.Trim(), o => o.ImmediateConsistency());
        return existing is null;
    }

    private async Task<IReadOnlyCollection<string>> ResolveOrganizationIdsAsync(string? organization)
    {
        if (String.IsNullOrWhiteSpace(organization))
            return [];

        string criteria = organization.Trim();
        var results = await organizationRepository.FindAsync(query => query
            .FieldOr(group => group
                .FieldEquals(item => item.Id, criteria)
                .FieldContains(item => item.Name, criteria))
            .SortAscending(item => item.Name), options => options.PageLimit(Pagination.MaximumLimit));
        return results.Documents.Select(item => item.Id).ToArray();
    }

    private async Task<IReadOnlyCollection<ViewOAuthApplication>> MapApplicationsAsync(IReadOnlyCollection<OAuthApplication> applications)
    {
        string[] organizationIds = applications
            .SelectMany(application => application.OrganizationIds)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var organizations = organizationIds.Length > 0
            ? await organizationRepository.GetByIdsAsync(organizationIds, options => options.Cache())
            : [];
        var organizationNames = organizations.ToDictionary(organization => organization.Id, organization => organization.Name, StringComparer.Ordinal);
        return applications.Select(application => ViewOAuthApplication.FromApplication(application, organizationNames)).ToArray();
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

    private static Result<T> ValidationResult<T>(MiniValidatorException ex)
    {
        return Result<T>.FromResult(Result.Invalid(ex.Errors.SelectMany(error =>
            error.Value.Select(message => ValidationError.Create(error.Key.ToLowerUnderscoredWords(), message)))));
    }
}
