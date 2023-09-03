﻿using AutoMapper;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Models;
using Foundatio.Caching;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Repositories.Extensions;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using McSherry.SemanticVersioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Web.Controllers;

[Route(API_PREFIX + "/stacks")]
[Authorize(Policy = AuthorizationRoles.ClientPolicy)]
public class StackController : RepositoryApiController<IStackRepository, Stack, Stack, Stack, Stack>
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IStackRepository _stackRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IWebHookRepository _webHookRepository;
    private readonly SemanticVersionParser _semanticVersionParser;
    private readonly WebHookDataPluginManager _webHookDataPluginManager;
    private readonly ICacheClient _cache;
    private readonly IQueue<WebHookNotification> _webHookNotificationQueue;
    private readonly BillingManager _billingManager;
    private readonly FormattingPluginManager _formattingPluginManager;
    private readonly AppOptions _options;

    public StackController(
        IStackRepository stackRepository,
        IOrganizationRepository organizationRepository,
        IProjectRepository projectRepository,
        IEventRepository eventRepository,
        IWebHookRepository webHookRepository,
        WebHookDataPluginManager webHookDataPluginManager,
        IQueue<WebHookNotification> webHookNotificationQueue,
        ICacheClient cacheClient,
        BillingManager billingManager,
        FormattingPluginManager formattingPluginManager,
        SemanticVersionParser semanticVersionParser,
        IMapper mapper,
        StackQueryValidator validator,
        AppOptions options,
        ILoggerFactory loggerFactory
    ) : base(stackRepository, mapper, validator, loggerFactory)
    {
        _stackRepository = stackRepository;
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _eventRepository = eventRepository;
        _webHookRepository = webHookRepository;
        _webHookDataPluginManager = webHookDataPluginManager;
        _webHookNotificationQueue = webHookNotificationQueue;
        _cache = cacheClient;
        _billingManager = billingManager;
        _formattingPluginManager = formattingPluginManager;
        _semanticVersionParser = semanticVersionParser;
        _options = options;

        AllowedDateFields.AddRange(new[] { StackIndex.Alias.FirstOccurrence, StackIndex.Alias.LastOccurrence });
        DefaultDateField = StackIndex.Alias.LastOccurrence;
    }

    /// <summary>
    /// Get by id
    /// </summary>
    /// <param name="id">The identifier of the stack.</param>
    /// <param name="offset">The time offset in minutes that controls what data is returned based on the `time` filter. This is used for time zone support.</param>
    /// <response code="404">The stack could not be found.</response>
    [HttpGet("{id:objectid}", Name = "GetStackById")]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    public async Task<ActionResult<Stack>> GetAsync(string id, string? offset = null)
    {
        var stack = await GetModelAsync(id);
        if (stack is null)
            return NotFound();

        return Ok(stack.ApplyOffset(GetOffset(offset)));
    }

    /// <summary>
    /// Mark fixed
    /// </summary>
    /// <param name="ids">A comma delimited list of stack identifiers.</param>
    /// <param name="version">A version number that the stack was fixed in.</param>
    /// <response code="404">One or more stacks could not be found.</response>
    [HttpPost("{ids:objectids}/mark-fixed")]
    [Consumes("application/json")]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult> MarkFixedAsync(string ids, string? version = null)
    {
        SemanticVersion? semanticVersion = null;

        if (!String.IsNullOrEmpty(version))
        {
            semanticVersion = _semanticVersionParser.Parse(version);
            if (semanticVersion is null)
                return BadRequest("Invalid semantic version");
        }

        var stacks = await GetModelsAsync(ids.FromDelimitedString(), false);
        if (!stacks.Any())
            return NotFound();

        if (stacks.Count > 0)
        {
            foreach (var stack in stacks)
                stack.MarkFixed(semanticVersion);

            await _stackRepository.SaveAsync(stacks);
        }

        return Ok();
    }

    /// <summary>
    /// This controller action is called by zapier to mark the stack as fixed.
    /// </summary>
    [HttpPost("~/api/v1/stack/markfixed")]
    [HttpPost("mark-fixed")]
    [Consumes("application/json")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<ActionResult> MarkFixedAsync(JObject data)
    {
        string? id = null;
        if (data.TryGetValue("ErrorStack", out var value))
            id = value.Value<string>();

        if (data.TryGetValue("Stack", out value))
            id = value.Value<string>();

        if (String.IsNullOrEmpty(id))
            return NotFound();

        if (id.StartsWith("http"))
            id = id.Substring(id.LastIndexOf('/') + 1);

        return await MarkFixedAsync(id);
    }

    /// <summary>
    /// Mark the selected stacks as snoozed
    /// </summary>
    /// <param name="ids">A comma delimited list of stack identifiers.</param>
    /// <param name="snoozeUntilUtc">A time that the stack should be snoozed until.</param>
    /// <response code="404">One or more stacks could not be found.</response>
    [HttpPost("{ids:objectids}/mark-snoozed")]
    [Consumes("application/json")]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<WorkInProgressResult>> SnoozeAsync(string ids, DateTime snoozeUntilUtc)
    {
        if (snoozeUntilUtc < SystemClock.UtcNow.AddMinutes(5))
            return BadRequest("Must snooze for at least 5 minutes.");

        var stacks = await GetModelsAsync(ids.FromDelimitedString(), false);
        if (!stacks.Any())
            return NotFound();

        if (stacks.Count > 0)
        {
            foreach (var stack in stacks)
            {
                stack.Status = StackStatus.Snoozed;
                stack.SnoozeUntilUtc = snoozeUntilUtc;
                stack.FixedInVersion = null;
                stack.DateFixed = null;
            }

            await _stackRepository.SaveAsync(stacks);
        }

        return Ok();
    }

    /// <summary>
    /// Add reference link
    /// </summary>
    /// <param name="id">The identifier of the stack.</param>
    /// <param name="url">The reference link.</param>
    /// <response code="400">Invalid reference link.</response>
    /// <response code="404">The stack could not be found.</response>
    [HttpPost("{id:objectid}/add-link")]
    [Consumes("application/json")]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    public async Task<IActionResult> AddLinkAsync(string id, ValueFromBody<string?> url)
    {
        if (String.IsNullOrWhiteSpace(url?.Value))
            return BadRequest();

        var stack = await GetModelAsync(id, false);
        if (stack is null)
            return NotFound();

        if (!stack.References.Contains(url.Value.Trim()))
        {
            stack.References.Add(url.Value.Trim());
            await _stackRepository.SaveAsync(stack);
        }

        return Ok();
    }

    /// <summary>
    /// This controller action is called by zapier to add a reference link to a stack.
    /// </summary>
    [HttpPost("~/api/v1/stack/addlink")]
    [HttpPost("add-link")]
    [Consumes("application/json")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> AddLinkAsync(JObject data)
    {
        string? id = null;
        if (data.TryGetValue("ErrorStack", out var value))
            id = value.Value<string>();

        if (data.TryGetValue("Stack", out value))
            id = value.Value<string>();

        if (String.IsNullOrEmpty(id))
            return NotFound();

        if (id.StartsWith("http"))
            id = id.Substring(id.LastIndexOf('/') + 1);

        string? url = data.GetValue("Link")?.Value<string>();
        return await AddLinkAsync(id, new ValueFromBody<string?>(url));
    }

    /// <summary>
    /// Remove reference link
    /// </summary>
    /// <param name="id">The identifier of the stack.</param>
    /// <param name="url">The reference link.</param>
    /// <response code="204">The reference link was removed.</response>
    /// <response code="400">Invalid reference link.</response>
    /// <response code="404">The stack could not be found.</response>
    [HttpPost("{id:objectid}/remove-link")]
    [Consumes("application/json")]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveLinkAsync(string id, ValueFromBody<string> url)
    {
        if (String.IsNullOrWhiteSpace(url?.Value))
            return BadRequest();

        var stack = await GetModelAsync(id, false);
        if (stack is null)
            return NotFound();

        if (stack.References.Contains(url.Value.Trim()))
        {
            stack.References.Remove(url.Value.Trim());
            await _stackRepository.SaveAsync(stack);
        }

        return StatusCode(StatusCodes.Status204NoContent);
    }

    /// <summary>
    /// Mark future occurrences as critical
    /// </summary>
    /// <param name="ids">A comma delimited list of stack identifiers.</param>
    /// <response code="404">One or more stacks could not be found.</response>
    [HttpPost("{ids:objectids}/mark-critical")]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    public async Task<IActionResult> MarkCriticalAsync(string ids)
    {
        var stacks = await GetModelsAsync(ids.FromDelimitedString(), false);
        if (!stacks.Any())
            return NotFound();

        stacks = stacks.Where(s => !s.OccurrencesAreCritical).ToList();
        if (stacks.Count > 0)
        {
            foreach (var stack in stacks)
                stack.OccurrencesAreCritical = true;

            await _stackRepository.SaveAsync(stacks);
        }

        return Ok();
    }

    /// <summary>
    /// Mark future occurrences as not critical
    /// </summary>
    /// <param name="ids">A comma delimited list of stack identifiers.</param>
    /// <response code="204">The stacks were marked as not critical.</response>
    /// <response code="404">One or more stacks could not be found.</response>
    [HttpDelete("{ids:objectids}/mark-critical")]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkNotCriticalAsync(string ids)
    {
        var stacks = await GetModelsAsync(ids.FromDelimitedString(), false);
        if (!stacks.Any())
            return NotFound();

        stacks = stacks.Where(s => s.OccurrencesAreCritical).ToList();
        if (stacks.Count > 0)
        {
            foreach (var stack in stacks)
                stack.OccurrencesAreCritical = false;

            await _stackRepository.SaveAsync(stacks);
        }

        return StatusCode(StatusCodes.Status204NoContent);
    }

    /// <summary>
    /// Change stack status
    /// </summary>
    /// <param name="ids">A comma delimited list of stack identifiers.</param>
    /// <param name="status">The status that the stack should be changed to.</param>
    /// <response code="404">One or more stacks could not be found.</response>
    [HttpPost("{ids:objectids}/change-status")]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    public async Task<IActionResult> ChangeStatusAsync(string ids, StackStatus status)
    {
        if (status == StackStatus.Regressed || status == StackStatus.Snoozed)
            return BadRequest("Can't set stack status to regressed or snoozed.");

        var stacks = await GetModelsAsync(ids.FromDelimitedString(), false);
        if (!stacks.Any())
            return NotFound();

        stacks = stacks.Where(s => s.Status != status).ToList();
        if (stacks.Count > 0)
        {
            foreach (var stack in stacks)
            {
                stack.Status = status;
                if (status == StackStatus.Fixed)
                {
                    stack.DateFixed = SystemClock.UtcNow;
                }
                else
                {
                    stack.DateFixed = null;
                    stack.FixedInVersion = null;
                }

                if (status != StackStatus.Snoozed)
                    stack.SnoozeUntilUtc = null;
            }

            await _stackRepository.SaveAsync(stacks);
        }

        return Ok();
    }

    /// <summary>
    /// Promote to external service
    /// </summary>
    /// <param name="id">The identifier of the stack.</param>
    /// <response code="404">The stack could not be found.</response>
    /// <response code="426">Promote to External is a premium feature used to promote an error stack to an external system.</response>
    /// <response code="501">"No promoted web hooks are configured for this project.</response>
    [HttpPost("{id:objectid}/promote")]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    public async Task<IActionResult> PromoteAsync(string id)
    {
        if (String.IsNullOrEmpty(id))
            return NotFound();

        var stack = await _stackRepository.GetByIdAsync(id);
        if (stack is null || !CanAccessOrganization(stack.OrganizationId))
            return NotFound();

        if (!await _billingManager.HasPremiumFeaturesAsync(stack.OrganizationId))
            return PlanLimitReached("Promote to External is a premium feature used to promote an error stack to an external system. Please upgrade your plan to enable this feature.");

        var promotedProjectHooks = (await _webHookRepository.GetByProjectIdAsync(stack.ProjectId)).Documents.Where(p => p.EventTypes.Contains(WebHookRepository.EventTypes.StackPromoted)).ToList();
        if (!promotedProjectHooks.Any())
            return NotImplemented("No promoted web hooks are configured for this project. Please add a promoted web hook to use this feature.");

        using var _ = _logger.BeginScope(new ExceptionlessState()
            .Organization(stack.OrganizationId)
            .Project(stack.ProjectId)
            .Tag("Promote")
            .Identity(CurrentUser?.EmailAddress)
            .Property("User", CurrentUser)
            .SetHttpContext(HttpContext));

        var organization = await GetOrganizationAsync(stack.OrganizationId);
        if (organization is null)
            return NotFound();
        var project = await GetProjectAsync(stack.ProjectId);
        if (project is null)
            return NotFound();

        foreach (var hook in promotedProjectHooks)
        {
            if (!hook.IsEnabled)
            {
                _logger.LogWarning("Unable to promote to disabled WebHook Id={WebHookId}, Url={WebHookUrl}", hook.Id, hook.Url);
                continue;
            }

            var context = new WebHookDataContext(hook, organization, project, stack, null, stack.TotalOccurrences == 1, stack.Status == StackStatus.Regressed);
            var data = await _webHookDataPluginManager.CreateFromStackAsync(context);
            if (data is null)
            {
                _logger.LogWarning("Unable to promote to WebHook with null payload Id={WebHookId}, Url={WebHookUrl}", hook.Id, hook.Url);
                continue;
            }

            await _webHookNotificationQueue.EnqueueAsync(new WebHookNotification
            {
                OrganizationId = stack.OrganizationId,
                ProjectId = stack.ProjectId,
                WebHookId = hook.Id,
                Url = hook.Url,
                Type = WebHookType.General,
                Data = data
            });
        }

        return Ok();
    }

    /// <summary>
    /// Remove
    /// </summary>
    /// <param name="ids">A comma delimited list of stack identifiers.</param>
    /// <response code="204">No Content.</response>
    /// <response code="400">One or more validation errors occurred.</response>
    /// <response code="404">One or more stacks were not found.</response>
    /// <response code="500">An error occurred while deleting one or more stacks.</response>
    [HttpDelete("{ids:objectids}")]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public Task<ActionResult<WorkInProgressResult>> DeleteAsync(string ids)
    {
        return DeleteImplAsync(ids.FromDelimitedString());
    }

    /// <summary>
    /// Get all
    /// </summary>
    /// <param name="filter">A filter that controls what data is returned from the server.</param>
    /// <param name="sort">Controls the sort order that the data is returned in. In this example -date returns the results descending by date.</param>
    /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
    /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
    /// <param name="mode">If no mode is set then the whole stack object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
    /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
    /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
    /// <response code="400">Invalid filter.</response>
    [HttpGet]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    public async Task<ActionResult<IReadOnlyCollection<Stack>>> GetAsync(string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int page = 1, int limit = 10)
    {
        var organizations = await GetSelectedOrganizationsAsync(_organizationRepository, _projectRepository, _stackRepository, filter);
        if (organizations.Count(o => !o.IsSuspended) == 0)
            return Ok(EmptyModels);

        var ti = GetTimeInfo(time, offset, organizations.GetRetentionUtcCutoff(_options.MaximumRetentionDays));
        var sf = new AppFilter(organizations) { IsUserOrganizationsFilter = true };
        return await GetInternalAsync(sf, ti, filter, sort, mode, page, limit);
    }

    private async Task<ActionResult<IReadOnlyCollection<Stack>>> GetInternalAsync(AppFilter sf, TimeInfo ti, string? filter = null, string? sort = null, string? mode = null, int page = 1, int limit = 10)
    {
        page = GetPage(page);
        limit = GetLimit(limit);
        int skip = GetSkip(page, limit);
        if (skip > MAXIMUM_SKIP)
            return Ok(EmptyModels);

        var pr = await _validator.ValidateQueryAsync(filter);
        if (!pr.IsValid)
            return BadRequest(pr.Message);

        sf.UsesPremiumFeatures = pr.UsesPremiumFeatures;

        try
        {
            var results = await _repository.FindAsync(q => q.AppFilter(ShouldApplySystemFilter(sf, filter) ? sf : null).FilterExpression(filter).SortExpression(sort).DateRange(ti.Range.UtcStart, ti.Range.UtcEnd, ti.Field), o => o.PageNumber(page).PageLimit(limit));

            var stacks = results.Documents.Select(s => s.ApplyOffset(ti.Offset)).ToList();
            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "summary", StringComparison.OrdinalIgnoreCase))
                return OkWithResourceLinks(await GetStackSummariesAsync(stacks, sf, ti), results.HasMore && !NextPageExceedsSkipLimit(page, limit), page);

            return OkWithResourceLinks(stacks, results.HasMore && !NextPageExceedsSkipLimit(page, limit), page);
        }
        catch (ApplicationException ex)
        {
            using (_logger.BeginScope(new ExceptionlessState().Property("Search Filter", new { SystemFilter = sf, UserFilter = filter, Time = ti, Page = page, Limit = limit }).Tag("Search").Identity(CurrentUser?.EmailAddress).Property("User", CurrentUser).SetHttpContext(HttpContext)))
                _logger.LogError(ex, "An error has occurred. Please check your search filter.");

            return BadRequest("An error has occurred. Please check your search filter.");
        }
    }

    /// <summary>
    /// Get by organization
    /// </summary>
    /// <param name="organizationId">The identifier of the organization.</param>
    /// <param name="filter">A filter that controls what data is returned from the server.</param>
    /// <param name="sort">Controls the sort order that the data is returned in. In this example -date returns the results descending by date.</param>
    /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
    /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
    /// <param name="mode">If no mode is set then the whole stack object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
    /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
    /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
    /// <response code="400">Invalid filter.</response>
    /// <response code="404">The organization could not be found.</response>
    /// <response code="426">Unable to view stack occurrences for the suspended organization.</response>
    [HttpGet("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/stacks")]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    public async Task<ActionResult<IReadOnlyCollection<Stack>>> GetByOrganizationAsync(string? organizationId = null, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int page = 1, int limit = 10)
    {
        var organization = await GetOrganizationAsync(organizationId);
        if (organization is null)
            return NotFound();

        if (organization.IsSuspended)
            return PlanLimitReached("Unable to view stack occurrences for the suspended organization.");

        var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff(_options.MaximumRetentionDays));
        var sf = new AppFilter(organization);
        return await GetInternalAsync(sf, ti, filter, sort, mode, page, limit);
    }

    /// <summary>
    /// Get by project
    /// </summary>
    /// <param name="projectId">The identifier of the project.</param>
    /// <param name="filter">A filter that controls what data is returned from the server.</param>
    /// <param name="sort">Controls the sort order that the data is returned in. In this example -date returns the results descending by date.</param>
    /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
    /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
    /// <param name="mode">If no mode is set then the whole stack object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
    /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
    /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
    /// <response code="400">Invalid filter.</response>
    /// <response code="404">The organization could not be found.</response>
    /// <response code="426">Unable to view stack occurrences for the suspended organization.</response>
    [HttpGet("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks")]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    public async Task<ActionResult<IReadOnlyCollection<Stack>>> GetByProjectAsync(string? projectId = null, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int page = 1, int limit = 10)
    {
        var project = await GetProjectAsync(projectId);
        if (project is null)
            return NotFound();

        var organization = await GetOrganizationAsync(project.OrganizationId);
        if (organization is null)
            return NotFound();

        if (organization.IsSuspended)
            return PlanLimitReached("Unable to view stack occurrences for the suspended organization.");

        var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff(project, _options.MaximumRetentionDays));
        var sf = new AppFilter(project, organization);
        return await GetInternalAsync(sf, ti, filter, sort, mode, page, limit);
    }

    private Task<Organization?> GetOrganizationAsync(string? organizationId, bool useCache = true)
    {
        if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
            return Task.FromResult<Organization?>(null);

        return _organizationRepository.GetByIdAsync(organizationId, o => o.Cache(useCache))!;
    }

    private async Task<Project?> GetProjectAsync(string? projectId, bool useCache = true)
    {
        if (String.IsNullOrEmpty(projectId))
            return null;

        var project = await _projectRepository.GetByIdAsync(projectId, o => o.Cache(useCache));
        if (project is null || !CanAccessOrganization(project.OrganizationId))
            return null;

        return project;
    }

    private async Task<ICollection<StackSummaryModel>> GetStackSummariesAsync(ICollection<Stack> stacks, AppFilter eventSystemFilter, TimeInfo ti)
    {
        if (stacks.Count == 0)
            return new List<StackSummaryModel>();

        var systemFilter = new RepositoryQuery<PersistentEvent>().AppFilter(eventSystemFilter).DateRange(ti.Range.UtcStart, ti.Range.UtcEnd, (PersistentEvent e) => e.Date).Index(ti.Range.UtcStart, ti.Range.UtcEnd);
        var stackTerms = await _eventRepository.CountAsync(q => q.SystemFilter(systemFilter).FilterExpression(String.Join(" OR ", stacks.Select(r => $"stack:{r.Id}"))).AggregationsExpression($"terms:(stack_id~{stacks.Count} cardinality:user sum:count~1 min:date max:date)"));
        return await GetStackSummariesAsync(stacks, stackTerms.Aggregations.Terms<string>("terms_stack_id").Buckets, eventSystemFilter, ti);
    }

    private async Task<ICollection<StackSummaryModel>> GetStackSummariesAsync(ICollection<Stack> stacks, IReadOnlyCollection<KeyedBucket<string>> stackTerms, AppFilter sf, TimeInfo ti)
    {
        if (stacks.Count == 0)
            return new List<StackSummaryModel>(0);

        var totalUsers = await GetUserCountByProjectIdsAsync(stacks, sf, ti.Range.UtcStart, ti.Range.UtcEnd);
        return stacks.Join(stackTerms, s => s.Id, tk => tk.Key, (stack, term) =>
        {
            var data = _formattingPluginManager.GetStackSummaryData(stack);
            var summary = new StackSummaryModel
            {
                Id = data.Id,
                TemplateKey = data.TemplateKey,
                Data = data.Data,
                Title = stack.Title,
                Status = stack.Status,
                FirstOccurrence = term.Aggregations.Min<DateTime>("min_date").Value,
                LastOccurrence = term.Aggregations.Max<DateTime>("max_date").Value,
                Total = (long)(term.Aggregations.Sum("sum_count").Value ?? term.Total.GetValueOrDefault()),

                Users = term.Aggregations.Cardinality("cardinality_user").Value.GetValueOrDefault(),
                TotalUsers = totalUsers.GetOrDefault(stack.ProjectId)
            };

            return summary;
        }).ToList();
    }

    private async Task<Dictionary<string, double>> GetUserCountByProjectIdsAsync(ICollection<Stack> stacks, AppFilter sf, DateTime utcStart, DateTime utcEnd)
    {
        var scopedCacheClient = new ScopedCacheClient(_cache, $"Project:user-count:{utcStart.Floor(TimeSpan.FromMinutes(15)).Ticks}-{utcEnd.Floor(TimeSpan.FromMinutes(15)).Ticks}");
        var projectIds = stacks.Select(s => s.ProjectId).Distinct().ToList();
        var cachedTotals = await scopedCacheClient.GetAllAsync<double>(projectIds);

        var totals = cachedTotals.Where(kvp => kvp.Value.HasValue).ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);
        if (totals.Count == projectIds.Count)
            return totals;

        var systemFilter = new RepositoryQuery<PersistentEvent>().AppFilter(sf).DateRange(utcStart, utcEnd, (PersistentEvent e) => e.Date).Index(utcStart, utcEnd);
        var projects = cachedTotals
            .Where(kvp => !kvp.Value.HasValue && stacks.Contains(s => s.ProjectId == kvp.Key))
            .Select(kvp => new Project { Id = kvp.Key, OrganizationId = stacks.First(s => s.ProjectId == kvp.Key).OrganizationId })
            .ToList();
        var countResult = await _eventRepository.CountAsync(q => q.SystemFilter(systemFilter).FilterExpression(projects.BuildFilter()).AggregationsExpression("terms:(project_id cardinality:user)"));

        // Cache all projects that have more than 10 users for 5 minutes.
        var projectTerms = countResult.Aggregations.Terms<string>("terms_project_id").Buckets;
        var aggregations = projectTerms.ToDictionary(t => t.Key, t => t.Aggregations.Cardinality("cardinality_user").Value.GetValueOrDefault());
        await scopedCacheClient.SetAllAsync(aggregations.Where(t => t.Value >= 10).ToDictionary(k => k.Key, v => v.Value), TimeSpan.FromMinutes(5));
        totals.AddRange(aggregations);

        return totals;
    }
}
