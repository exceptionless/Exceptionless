using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Mapping;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Mediator;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using PermissionResult = Exceptionless.Web.Controllers.PermissionResult;
using DataDictionary = Exceptionless.Core.Models.DataDictionary;

namespace Exceptionless.Web.Api.Handlers;

public class ProjectHandler(
    IOrganizationRepository organizationRepository,
    IProjectRepository repository,
    IStackRepository stackRepository,
    IEventRepository eventRepository,
    ITokenRepository tokenRepository,
    IQueue<WorkItemData> workItemQueue,
    BillingManager billingManager,
    SlackService slackService,
    SampleDataService sampleDataService,
    ApiMapper mapper,
    AppOptions options,
    UsageService usageService,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<ProjectHandler>();

    public async Task<Result<PagedResult<ViewProject>>> Handle(GetProjects message)
    {
        var organizations = await GetSelectedOrganizationsAsync(message.Context, message.Filter);
        if (organizations.Count == 0)
            return new PagedResult<ViewProject>(Array.Empty<ViewProject>(), false, 1, 0);

        int page = Pagination.GetPage(message.Page);
        int limit = Pagination.GetLimit(message.Limit, Pagination.MaximumSkip);
        var sf = new AppFilter(organizations) { IsUserOrganizationsFilter = true };
        var projects = await repository.GetByFilterAsync(sf, message.Filter, message.Sort, o => o.PageNumber(page).PageLimit(limit));
        var viewProjects = mapper.MapToViewProjects(projects.Documents);
        await AfterResultMapAsync(viewProjects);

        if (IsStatsMode(message.Mode))
            return new PagedResult<ViewProject>(await PopulateProjectStatsAsync(viewProjects), projects.HasMore && !Pagination.NextPageExceedsSkipLimit(page, limit), page, projects.Total);

        return new PagedResult<ViewProject>(viewProjects, projects.HasMore && !Pagination.NextPageExceedsSkipLimit(page, limit), page, projects.Total);
    }

    public async Task<Result<PagedResult<ViewProject>>> Handle(GetProjectsByOrganization message)
    {
        var organization = await GetOrganizationAsync(message.OrganizationId, message.Context);
        if (organization is null)
            return Result.NotFound("Project not found.");

        int page = Pagination.GetPage(message.Page);
        int limit = Pagination.GetLimit(message.Limit, Pagination.MaximumSkip);
        var sf = new AppFilter(organization);
        var projects = await repository.GetByFilterAsync(sf, message.Filter, message.Sort, o => o.PageNumber(page).PageLimit(limit));
        var viewProjects = mapper.MapToViewProjects(projects.Documents);
        await AfterResultMapAsync(viewProjects);

        if (IsStatsMode(message.Mode))
            return new PagedResult<ViewProject>(await PopulateProjectStatsAsync(viewProjects), projects.HasMore && !Pagination.NextPageExceedsSkipLimit(page, limit), page, projects.Total);

        return new PagedResult<ViewProject>(viewProjects, projects.HasMore && !Pagination.NextPageExceedsSkipLimit(page, limit), page, projects.Total);
    }

    public async Task<Result<ViewProject>> Handle(GetProjectById message)
    {
        var project = await GetModelAsync(message.Id, message.Context);
        if (project is null)
            return Result.NotFound("Project not found.");

        var viewProject = mapper.MapToViewProject(project);
        await AfterResultMapAsync([viewProject]);

        if (IsStatsMode(message.Mode))
            return await PopulateProjectStatsAsync(viewProject);

        return viewProject;
    }

    public async Task<Result<ViewProject>> Handle(CreateProject message)
    {
        if (message.Project is null)
            return Result.BadRequest("Project value is required.");

        var model = mapper.MapToProject(message.Project);
        if (String.IsNullOrEmpty(model.OrganizationId) && message.Context.Request.GetAssociatedOrganizationIds().Count > 0)
            model.OrganizationId = message.Context.Request.GetDefaultOrganizationId()!;

        var error = await CanAddAsync(model, message.Context);
        if (error is not null)
            return error;

        model = await AddModelAsync(model, message.Context);
        var viewModel = mapper.MapToViewProject(model);
        await AfterResultMapAsync([viewModel]);
        return Result<ViewProject>.Created(viewModel, $"/api/v2/projects/{model.Id}");
    }

    public async Task<Result<ViewProject>> Handle(UpdateProjectMessage message)
    {
        var original = await GetModelAsync(message.Id, message.Context, useCache: false);
        if (original is null)
            return Result.NotFound("Project not found.");

        if (message.PatchDocument.IsEmpty())
            return await MapToViewAsync(original);

        var validationResult = JsonPatchValidation.ValidateOperations(message.PatchDocument, "/organization_id");
        if (!validationResult.IsSuccess)
            return Result<ViewProject>.FromResult(validationResult);

        var dto = new UpdateProject {
            Name = original.Name,
            DeleteBotDataEnabled = original.DeleteBotDataEnabled
        };

        var patchResult = JsonPatchValidation.ApplyPatch(message.PatchDocument, dto);
        if (!patchResult.IsSuccess)
            return Result<ViewProject>.FromResult(patchResult);

        var error = await CanUpdateAsync(original, dto, message.PatchDocument, message.Context);
        if (error is not null)
            return error;

        original.Name = dto.Name;
        original.DeleteBotDataEnabled = dto.DeleteBotDataEnabled;

        await repository.SaveAsync(original, o => o.Cache());
        return await MapToViewAsync(original);
    }

    public async Task<Result<ModelActionResults>> Handle(DeleteProjects message)
    {
        var items = await GetModelsAsync(message.Ids, message.Context, useCache: false);
        if (items.Count == 0)
            return Result.NotFound("Project not found.");

        var results = new ModelActionResults();
        results.AddNotFound(message.Ids.Except(items.Select(i => i.Id)));

        var deletableItems = items.ToList();
        foreach (var model in items)
        {
            var permission = await CanDeleteAsync(model, message.Context);
            if (permission.Allowed)
                continue;

            deletableItems.Remove(model);
            results.Failure.Add(permission);
        }

        if (deletableItems.Count == 0)
            return results.Failure.Count == 1 ? Result<ModelActionResults>.FromResult(PermissionToResult(results.Failure.First())) : results;

        IEnumerable<string> workIds = await DeleteModelsAsync(deletableItems, message.Context);
        if (results.Failure.Count == 0)
            return new ModelActionResults { Workers = workIds.ToList() };

        results.Workers.AddRange(workIds);
        results.Success.AddRange(deletableItems.Select(i => i.Id));
        return results;
    }

    public Task<Result<object>> Handle(GetLegacyProjectConfig message)
    {
        return GetConfigAsync(null, message.Version, message.Context);
    }

    public Task<Result<object>> Handle(GetProjectConfig message)
    {
        return GetConfigAsync(message.Id, message.Version, message.Context);
    }

    public async Task<Result> Handle(SetProjectConfig message)
    {
        if (String.IsNullOrWhiteSpace(message.Key) || String.IsNullOrWhiteSpace(message.Value?.Value))
            return Result.BadRequest("Invalid configuration value.");

        var project = await GetModelAsync(message.Id, message.Context, useCache: false);
        if (project is null)
            return Result.NotFound("Project not found.");

        project.Configuration.Settings[message.Key.Trim()] = message.Value.Value.Trim();
        project.Configuration.IncrementVersion();
        await repository.SaveAsync(project, o => o.Cache());
        return Result.Success();
    }

    public async Task<Result> Handle(DeleteProjectConfig message)
    {
        if (String.IsNullOrWhiteSpace(message.Key))
            return Result.BadRequest("Invalid key value.");

        var project = await GetModelAsync(message.Id, message.Context, useCache: false);
        if (project is null)
            return Result.NotFound("Project not found.");

        if (project.Configuration.Settings.Remove(message.Key.Trim()))
        {
            project.Configuration.IncrementVersion();
            await repository.SaveAsync(project, o => o.Cache());
        }

        return Result.Success();
    }

    public async Task<Result<WorkInProgressResult>> Handle(GenerateProjectSampleData message)
    {
        var project = await GetModelAsync(message.Id, message.Context);
        if (project is null)
            return Result.NotFound("Project not found.");

        string workItemId = await sampleDataService.EnqueueSampleEventsAsync(project.OrganizationId, project.Id);
        return new WorkInProgressResult([workItemId]);
    }

    public async Task<Result<WorkInProgressResult>> Handle(ResetProjectData message)
    {
        var project = await GetModelAsync(message.Id, message.Context);
        if (project is null)
            return Result.NotFound("Project not found.");

        string workItemId = await workItemQueue.EnqueueAsync(new ResetProjectDataWorkItem
        {
            OrganizationId = project.OrganizationId,
            ProjectId = project.Id
        });

        return new WorkInProgressResult([workItemId]);
    }

    public async Task<Result<IDictionary<string, NotificationSettings>>> Handle(GetProjectNotificationSettings message)
    {
        var project = await GetModelAsync(message.Id, message.Context);
        if (project is null)
            return Result.NotFound("Project not found.");

        return project.NotificationSettings;
    }

    public async Task<Result<NotificationSettings>> Handle(GetProjectUserNotificationSettings message)
    {
        var project = await GetModelAsync(message.Id, message.Context);
        if (project is null)
            return Result.NotFound("Project not found.");

        if (!message.Context.Request.IsGlobalAdmin() && !String.Equals(GetCurrentUserId(message.Context), message.UserId, StringComparison.Ordinal))
            return Result.NotFound("Project not found.");

        return project.NotificationSettings.TryGetValue(message.UserId, out var settings) ? settings : new NotificationSettings();
    }

    public async Task<Result<NotificationSettings>> Handle(GetProjectIntegrationNotificationSettings message)
    {
        var project = await GetModelAsync(message.Id, message.Context);
        if (project is null)
            return Result.NotFound("Project not found.");

        if (!String.Equals(Project.NotificationIntegrations.Slack, message.Integration, StringComparison.Ordinal))
            return Result.NotFound("Project not found.");

        return project.NotificationSettings.TryGetValue(Project.NotificationIntegrations.Slack, out var settings) ? settings : new NotificationSettings();
    }

    public async Task<Result> Handle(SetProjectUserNotificationSettings message)
    {
        var project = await GetModelAsync(message.Id, message.Context, useCache: false);
        if (project is null)
            return Result.NotFound("Project not found.");

        if (!message.Context.Request.IsGlobalAdmin() && !String.Equals(GetCurrentUserId(message.Context), message.UserId, StringComparison.Ordinal))
            return Result.NotFound("Project not found.");

        if (message.Settings is null)
            project.NotificationSettings.Remove(message.UserId);
        else
            project.NotificationSettings[message.UserId] = message.Settings;

        await repository.SaveAsync(project, o => o.Cache());
        return Result.Success();
    }

    public async Task<Result> Handle(SetProjectIntegrationNotificationSettings message)
    {
        if (!String.Equals(Project.NotificationIntegrations.Slack, message.Integration, StringComparison.Ordinal))
            return Result.NotFound("Project not found.");

        var project = await GetModelAsync(message.Id, message.Context, useCache: false);
        if (project is null)
            return Result.NotFound("Project not found.");

        var organization = await organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache());
        if (organization is null)
            return Result.NotFound("Project not found.");

        if (!organization.HasPremiumFeatures)
            return Result.Invalid(ValidationError.Create("plan_limit", $"Please upgrade your plan to enable {message.Integration} integration."));

        if (message.Settings is null)
            project.NotificationSettings.Remove(message.Integration);
        else
            project.NotificationSettings[message.Integration] = message.Settings;

        await repository.SaveAsync(project, o => o.Cache());
        return Result.Success();
    }

    public async Task<Result> Handle(DeleteProjectNotificationSettings message)
    {
        var project = await GetModelAsync(message.Id, message.Context, useCache: false);
        if (project is null)
            return Result.NotFound("Project not found.");

        if (!message.Context.Request.IsGlobalAdmin() && !String.Equals(GetCurrentUserId(message.Context), message.UserId, StringComparison.Ordinal))
            return Result.NotFound("Project not found.");

        if (project.NotificationSettings.Remove(message.UserId))
            await repository.SaveAsync(project, o => o.Cache());

        return Result.Success();
    }

    public async Task<Result> Handle(PromoteProjectTab message)
    {
        if (String.IsNullOrWhiteSpace(message.Name))
            return Result.BadRequest("Invalid tab name.");

        var project = await GetModelAsync(message.Id, message.Context, useCache: false);
        if (project is null)
            return Result.NotFound("Project not found.");

        project.PromotedTabs ??= [];
        if (project.PromotedTabs.Add(message.Name.Trim()))
            await repository.SaveAsync(project, o => o.Cache());

        return Result.Success();
    }

    public async Task<Result> Handle(DemoteProjectTab message)
    {
        if (String.IsNullOrWhiteSpace(message.Name))
            return Result.BadRequest("Invalid tab name.");

        var project = await GetModelAsync(message.Id, message.Context, useCache: false);
        if (project is null)
            return Result.NotFound("Project not found.");

        if (project.PromotedTabs is not null && project.PromotedTabs.Remove(message.Name.Trim()))
            await repository.SaveAsync(project, o => o.Cache());

        return Result.Success();
    }

    public async Task<Result> Handle(CheckProjectName message)
    {
        if (await IsProjectNameAvailableInternalAsync(message.OrganizationId, message.Name, message.Context))
            return Result.NoContent();

        return Result.Created();
    }

    public async Task<Result> Handle(SetProjectData message)
    {
        if (String.IsNullOrWhiteSpace(message.Key) || String.IsNullOrWhiteSpace(message.Value?.Value) || message.Key.StartsWith('-'))
            return Result.BadRequest("Invalid key or value.");

        var project = await GetModelAsync(message.Id, message.Context, useCache: false);
        if (project is null)
            return Result.NotFound("Project not found.");

        project.Data ??= new DataDictionary();
        project.Data[message.Key.Trim()] = message.Value.Value.Trim();
        await repository.SaveAsync(project, o => o.Cache());

        return Result.Success();
    }

    public async Task<Result> Handle(DeleteProjectData message)
    {
        if (String.IsNullOrWhiteSpace(message.Key) || message.Key.StartsWith('-'))
            return Result.BadRequest("Invalid key value.");

        var project = await GetModelAsync(message.Id, message.Context, useCache: false);
        if (project is null)
            return Result.NotFound("Project not found.");

        if (project.Data is not null && project.Data.Remove(message.Key.Trim()))
            await repository.SaveAsync(project, o => o.Cache());

        return Result.Success();
    }

    public async Task<Result<object>> Handle(AddProjectSlack message)
    {
        if (String.IsNullOrWhiteSpace(message.Code))
            return Result.BadRequest("Invalid Slack authorization code.");

        var project = await GetModelAsync(message.Id, message.Context, useCache: false);
        if (project is null)
            return Result.NotFound("Project not found.");

        using var _ = _logger.BeginScope(new ExceptionlessState().Organization(project.OrganizationId).Project(project.Id).Property("Code", message.Code).Tag("Slack").Identity(GetCurrentUser(message.Context).EmailAddress).Property("User", GetCurrentUser(message.Context)).SetHttpContext(message.Context));

        if (project.Data is not null && project.Data.ContainsKey(Project.KnownDataKeys.SlackToken))
            return new NotModifiedResponse();

        SlackToken? token;
        try
        {
            token = await slackService.GetAccessTokenAsync(message.Code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting slack access token: {Message}", ex.Message);
            throw;
        }

        project.AddDefaultNotificationSettings(Project.NotificationIntegrations.Slack);
        project.Data ??= new DataDictionary();
        project.Data[Project.KnownDataKeys.SlackToken] = token;
        await repository.SaveAsync(project, o => o.Cache());

        return Result.Success().Cast<object>();
    }

    public async Task<Result> Handle(RemoveProjectSlack message)
    {
        var project = await GetModelAsync(message.Id, message.Context, useCache: false);
        if (project is null)
            return Result.NotFound("Project not found.");

        var token = project.GetSlackToken();
        using var _ = _logger.BeginScope(new ExceptionlessState().Property("Token", token).Tag("Slack").Identity(GetCurrentUser(message.Context).EmailAddress).Property("User", GetCurrentUser(message.Context)).SetHttpContext(message.Context));

        if (token is not null)
            await slackService.RevokeAccessTokenAsync(token.AccessToken);

        bool shouldSave = project.NotificationSettings.Remove(Project.NotificationIntegrations.Slack);
        if (project.Data is not null && project.Data.Remove(Project.KnownDataKeys.SlackToken))
            shouldSave = true;

        if (shouldSave)
            await repository.SaveAsync(project, o => o.Cache());

        return Result.Success();
    }

    private async Task<Result<object>> GetConfigAsync(string? id, int? version, HttpContext httpContext)
    {
        if (String.IsNullOrEmpty(id))
            id = httpContext.User.GetProjectId();

        var project = await repository.GetConfigAsync(id);
        if (project is null)
            return Result.NotFound("Project not found.");

        if (!httpContext.Request.CanAccessOrganization(project.OrganizationId))
            return Result.NotFound("Project not found.");

        if (version.HasValue && version == project.Configuration.Version)
            return new NotModifiedResponse();

        return project.Configuration;
    }

    private async Task<ViewProject> MapToViewAsync(Project model)
    {
        var viewModel = mapper.MapToViewProject(model);
        await AfterResultMapAsync([viewModel]);
        return viewModel;
    }

    private async Task AfterResultMapAsync<TDestination>(ICollection<TDestination> models)
    {
        foreach (var model in models.OfType<IData>())
            model.Data?.RemoveSensitiveData();

        var viewProjects = models.OfType<ViewProject>().ToList();
        if (viewProjects.Count == 0)
            return;

        var organizations = await organizationRepository.GetByIdsAsync(viewProjects.Select(p => p.OrganizationId).Distinct().ToArray(), o => o.Cache());
        foreach (var viewProject in viewProjects)
        {
            if (!viewProject.IsConfigured.HasValue)
            {
                viewProject.IsConfigured = true;
                await workItemQueue.EnqueueAsync(new SetProjectIsConfiguredWorkItem { ProjectId = viewProject.Id });
            }

            var organization = organizations.SingleOrDefault(o => o.Id == viewProject.OrganizationId);
            if (organization is null)
                continue;

            viewProject.OrganizationName = organization.Name;
            viewProject.HasPremiumFeatures = organization.HasPremiumFeatures;

            var realTimeUsage = await usageService.GetUsageAsync(organization.Id, viewProject.Id);
            viewProject.EnsureUsage(organization.GetMaxEventsPerMonthWithBonus(timeProvider), timeProvider);
            viewProject.TrimUsage(timeProvider);

            var currentUsage = viewProject.GetCurrentUsage(organization.GetMaxEventsPerMonthWithBonus(timeProvider), timeProvider);
            currentUsage.Limit = realTimeUsage.CurrentUsage.Limit;
            currentUsage.Total = realTimeUsage.CurrentUsage.Total;
            currentUsage.Blocked = realTimeUsage.CurrentUsage.Blocked;
            currentUsage.Discarded = realTimeUsage.CurrentUsage.Discarded;
            currentUsage.TooBig = realTimeUsage.CurrentUsage.TooBig;
            currentUsage.Deleted = realTimeUsage.CurrentUsage.Deleted;

            var currentHourUsage = viewProject.GetCurrentHourlyUsage(timeProvider);
            currentHourUsage.Total = realTimeUsage.CurrentHourUsage.Total;
            currentHourUsage.Blocked = realTimeUsage.CurrentHourUsage.Blocked;
            currentHourUsage.Discarded = realTimeUsage.CurrentHourUsage.Discarded;
            currentHourUsage.TooBig = realTimeUsage.CurrentHourUsage.TooBig;
            currentHourUsage.Deleted = realTimeUsage.CurrentHourUsage.Deleted;
        }
    }

    private async Task<Result<ViewProject>?> CanAddAsync(Project value, HttpContext httpContext)
    {
        if (String.IsNullOrEmpty(value.Name))
            return Result.BadRequest("Project name is required.");

        if (!await IsProjectNameAvailableInternalAsync(value.OrganizationId, value.Name, httpContext))
            return Result.BadRequest("A project with this name already exists.");

        if (!await billingManager.CanAddProjectAsync(value))
            return Result.Invalid(ValidationError.Create("plan_limit", "Please upgrade your plan to add additional projects."));

        if (!httpContext.Request.CanAccessOrganization(value.OrganizationId))
            return Result.BadRequest("Invalid organization id specified.");

        return null;
    }

    private Task<Project> AddModelAsync(Project value, HttpContext httpContext)
    {
        value.IsConfigured = false;
        value.NextSummaryEndOfDayTicks = timeProvider.GetUtcNow().UtcDateTime.Date.AddDays(1).AddHours(1).Ticks;
        value.AddDefaultNotificationSettings(GetCurrentUserId(httpContext));
        value.SetDefaultUserAgentBotPatterns();
        value.Configuration.IncrementVersion();
        return repository.AddAsync(value, o => o.Cache());
    }

    private async Task<Result<ViewProject>?> CanUpdateAsync(Project original, UpdateProject dto, JsonPatchDocument<UpdateProject> patch, HttpContext httpContext)
    {
        if (patch.AffectsProperty(p => p.Name) && !await IsProjectNameAvailableInternalAsync(original.OrganizationId, dto.Name, httpContext))
            return Result.BadRequest("A project with this name already exists.");

        if (!httpContext.Request.CanAccessOrganization(original.OrganizationId))
            return Result.BadRequest("Invalid organization id specified.");

        if (patch.AffectsPath("/organization_id"))
            return Result.BadRequest("OrganizationId cannot be modified.");

        return null;
    }

    private Task<PermissionResult> CanDeleteAsync(Project value, HttpContext httpContext)
    {
        if (!httpContext.Request.CanAccessOrganization(value.OrganizationId))
            return Task.FromResult(PermissionResult.DenyWithNotFound(value.Id));

        return Task.FromResult(PermissionResult.Allow);
    }

    private async Task<IEnumerable<string>> DeleteModelsAsync(ICollection<Project> projects, HttpContext httpContext)
    {
        var user = GetCurrentUser(httpContext);
        foreach (var project in projects)
        {
            using var _ = _logger.BeginScope(new ExceptionlessState().Organization(project.OrganizationId).Project(project.Id).Tag("Delete").Identity(user.EmailAddress).Property("User", user).SetHttpContext(httpContext));
            _logger.UserDeletingProject(user.Id, project.Name);
            await tokenRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id);
        }

        foreach (var project in projects.OfType<ISupportSoftDeletes>())
            project.IsDeleted = true;

        await repository.SaveAsync(projects);
        return [];
    }

    private async Task<Project?> GetModelAsync(string id, HttpContext httpContext, bool useCache = true)
    {
        if (String.IsNullOrEmpty(id))
            return null;

        var model = await repository.GetByIdAsync(id, o => o.Cache(useCache));
        if (model is null)
            return null;

        if (!httpContext.Request.CanAccessOrganization(model.OrganizationId))
            return null;

        return model;
    }

    private async Task<IReadOnlyCollection<Project>> GetModelsAsync(string[] ids, HttpContext httpContext, bool useCache = true)
    {
        if (ids.Length == 0)
            return [];

        var models = await repository.GetByIdsAsync(ids, o => o.Cache(useCache));
        return models.Where(m => httpContext.Request.CanAccessOrganization(m.OrganizationId)).ToList();
    }

    private Task<Organization?> GetOrganizationAsync(string organizationId, HttpContext httpContext, bool useCache = true)
    {
        if (String.IsNullOrEmpty(organizationId) || !httpContext.Request.CanAccessOrganization(organizationId))
            return Task.FromResult<Organization?>(null);

        return organizationRepository.GetByIdAsync(organizationId, o => o.Cache(useCache));
    }

    private async Task<IReadOnlyCollection<Organization>> GetSelectedOrganizationsAsync(HttpContext httpContext, string? filter = null)
    {
        var associatedOrganizationIds = httpContext.Request.GetAssociatedOrganizationIds();
        if (associatedOrganizationIds.Count == 0)
            return Array.Empty<Organization>();

        if (!String.IsNullOrEmpty(filter))
        {
            var scope = GetFilterScopeVisitor.Run(filter);
            if (scope.IsScopable)
            {
                Organization? organization = null;
                if (scope.OrganizationId is not null)
                {
                    organization = await organizationRepository.GetByIdAsync(scope.OrganizationId, o => o.Cache());
                }
                else if (scope.ProjectId is not null)
                {
                    var project = await repository.GetByIdAsync(scope.ProjectId, o => o.Cache());
                    if (project is not null)
                        organization = await organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache());
                }
                else if (scope.StackId is not null)
                {
                    var stack = await stackRepository.GetByIdAsync(scope.StackId, o => o.Cache());
                    if (stack is not null)
                        organization = await organizationRepository.GetByIdAsync(stack.OrganizationId, o => o.Cache());
                }

                if (organization is not null)
                {
                    if (associatedOrganizationIds.Contains(organization.Id) || httpContext.Request.IsGlobalAdmin())
                        return new[] { organization };

                    return Array.Empty<Organization>();
                }
            }
        }

        return await organizationRepository.GetByIdsAsync(associatedOrganizationIds.ToArray(), o => o.Cache());
    }

    private async Task<bool> IsProjectNameAvailableInternalAsync(string? organizationId, string name, HttpContext httpContext)
    {
        if (String.IsNullOrWhiteSpace(name))
            return false;

        var organizationIds = organizationId is not null && httpContext.Request.IsInOrganization(organizationId)
            ? new[] { organizationId }
            : httpContext.Request.GetAssociatedOrganizationIds().ToArray();
        var projects = await repository.GetByOrganizationIdsAsync(organizationIds);
        string decodedName = Uri.UnescapeDataString(name).Trim().ToLowerInvariant();
        return !projects.Documents.Any(p => String.Equals(p.Name.Trim().ToLowerInvariant(), decodedName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<ViewProject> PopulateProjectStatsAsync(ViewProject project)
    {
        return (await PopulateProjectStatsAsync([project])).Single();
    }

    private async Task<List<ViewProject>> PopulateProjectStatsAsync(List<ViewProject> viewProjects)
    {
        if (viewProjects.Count == 0)
            return viewProjects;

        int maximumRetentionDays = options.MaximumRetentionDays;
        var organizations = await organizationRepository.GetByIdsAsync(viewProjects.Select(p => p.OrganizationId).ToArray(), o => o.Cache());
        var projects = viewProjects.Select(p => new Project { Id = p.Id, CreatedUtc = p.CreatedUtc, OrganizationId = p.OrganizationId }).ToList();
        var sf = new AppFilter(projects, organizations);
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var retentionUtcCutoff = organizations.GetRetentionUtcCutoff(maximumRetentionDays, timeProvider);
        var systemFilter = new RepositoryQuery<PersistentEvent>()
            .AppFilter(sf)
            .DateRange(retentionUtcCutoff, utcNow, (PersistentEvent e) => e.Date)
            .Index(retentionUtcCutoff, utcNow);
        var result = await eventRepository.CountAsync(q => q
            .SystemFilter(systemFilter)
            .AggregationsExpression($"terms:(project_id~{viewProjects.Count} cardinality:stack_id)")
            .EnforceEventStackFilter(false));

        foreach (var project in viewProjects)
        {
            var term = result.Aggregations.Terms<string>("terms_project_id")?.Buckets.FirstOrDefault(t => t.Key == project.Id);
            project.EventCount = term?.Total ?? 0;
            project.StackCount = (long)(term?.Aggregations.Cardinality("cardinality_stack_id")?.Value ?? 0);
        }

        return viewProjects;
    }

    private static Result PermissionToResult(PermissionResult permission)
    {
        if (permission.StatusCode == StatusCodes.Status404NotFound)
            return Result.NotFound(permission.Message ?? "Project not found.");

        if (permission.StatusCode == StatusCodes.Status422UnprocessableEntity)
            return Result.Invalid(ValidationError.Create("general", permission.Message ?? "Validation failed."));

        return Result.Forbidden(permission.Message ?? "Access denied.");
    }

    private static User GetCurrentUser(HttpContext httpContext) => httpContext.Request.GetUser();
    private static string GetCurrentUserId(HttpContext httpContext) => GetCurrentUser(httpContext).Id;
    private static bool IsStatsMode(string? mode) => !String.IsNullOrEmpty(mode) && String.Equals(mode, "stats", StringComparison.OrdinalIgnoreCase);
}
