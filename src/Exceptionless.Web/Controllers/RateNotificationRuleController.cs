using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Foundatio.Repositories;

namespace Exceptionless.App.Controllers.API;

/// <summary>
/// Personal rate notification rule management.
/// </summary>
[Route(API_PREFIX + "/users/{userId:objectid}/projects/{projectId:objectid}/rate-notifications")]
[Authorize(Policy = AuthorizationRoles.UserPolicy)]
public class RateNotificationRuleController : ExceptionlessApiController
{
    private const int MaxRulesPerUserPerProject = 20;

    private static readonly TimeSpan[] ValidWindows =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1)
    ];

    private readonly IRateNotificationRuleRepository _ruleRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IStackRepository _stackRepository;
    private readonly RateNotificationRuleCache _ruleCache;

    public RateNotificationRuleController(
        IRateNotificationRuleRepository ruleRepository,
        IProjectRepository projectRepository,
        IOrganizationRepository organizationRepository,
        IStackRepository stackRepository,
        RateNotificationRuleCache ruleCache,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory) : base(timeProvider)
    {
        _ruleRepository = ruleRepository;
        _projectRepository = projectRepository;
        _organizationRepository = organizationRepository;
        _stackRepository = stackRepository;
        _ruleCache = ruleCache;
    }

    /// <summary>Get all rate notification rules for a user/project.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ViewRateNotificationRule>>> GetAsync(
        string userId,
        string projectId,
        int page = 1,
        int limit = 25)
    {
        if (!CanManage(userId))
            return NotFound();

        var project = await GetProjectAndCheckAccessAsync(projectId);
        if (project is null)
            return NotFound();

        page = GetPage(page);
        limit = GetLimit(limit);

        var results = await _ruleRepository.GetByProjectIdAndUserIdAsync(projectId, userId, o => o.PageNumber(page).PageLimit(limit));
        var viewModels = results.Documents.Select(MapToView).ToList();
        return OkWithResourceLinks(viewModels, results.HasMore && !NextPageExceedsSkipLimit(page, limit), page, results.Total);
    }

    /// <summary>Create a rate notification rule.</summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ViewRateNotificationRule>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ViewRateNotificationRule>> PostAsync(
        string userId,
        string projectId,
        [FromBody] NewRateNotificationRule model)
    {
        if (!CanManage(userId))
            return NotFound();

        var project = await GetProjectAndCheckAccessAsync(projectId);
        if (project is null)
            return NotFound();

        // Validate window
        if (!ValidWindows.Contains(model.Window))
            return ValidationProblem(detail: $"Window must be one of: {String.Join(", ", ValidWindows.Select(w => w.ToString()))}");

        // Validate cooldown >= window
        if (model.Cooldown < model.Window)
            return ValidationProblem(detail: "Cooldown must be greater than or equal to Window.");

        // Validate subject / stackId
        if (model.Subject == RateNotificationSubject.Stack)
        {
            if (String.IsNullOrEmpty(model.StackId))
                return ValidationProblem(detail: "StackId is required when Subject is Stack.");

            var stack = await _stackRepository.GetByIdAsync(model.StackId, o => o.Cache());
            if (stack is null || !String.Equals(stack.ProjectId, projectId, StringComparison.Ordinal))
                return ValidationProblem(detail: "The specified StackId does not belong to this project.");
        }
        else if (!String.IsNullOrEmpty(model.StackId))
        {
            return ValidationProblem(detail: "StackId must be empty when Subject is Project.");
        }

        // Enforce max rules limit
        long count = await _ruleRepository.CountByProjectIdAndUserIdAsync(projectId, userId);
        if (count >= MaxRulesPerUserPerProject)
            return ValidationProblem(detail: $"Maximum of {MaxRulesPerUserPerProject} rate notification rules per user per project.");

        // Premium gate — non-premium users can create rules but they start disabled
        var org = await _organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache());
        bool isEnabled = model.IsEnabled;
        if (!org?.HasPremiumFeatures ?? false)
            isEnabled = false;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var rule = new RateNotificationRule
        {
            OrganizationId = project.OrganizationId,
            ProjectId = projectId,
            UserId = userId,
            Name = model.Name,
            IsEnabled = isEnabled,
            Signal = model.Signal,
            Subject = model.Subject,
            StackId = model.StackId,
            Threshold = model.Threshold,
            Window = model.Window,
            Cooldown = model.Cooldown,
            Version = 1,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        rule = await _ruleRepository.AddAsync(rule, o => o.Cache());
        await _ruleCache.InvalidateAsync(projectId);

        return Created(new Uri(Url.Link("GetRateNotificationRuleById", new { userId, projectId, ruleId = rule.Id })!, UriKind.RelativeOrAbsolute), MapToView(rule));
    }

    /// <summary>Get a specific rate notification rule.</summary>
    [HttpGet("{ruleId:objectid}", Name = "GetRateNotificationRuleById")]
    public async Task<ActionResult<ViewRateNotificationRule>> GetByIdAsync(string userId, string projectId, string ruleId)
    {
        if (!CanManage(userId))
            return NotFound();

        var rule = await GetRuleAndCheckAccessAsync(ruleId, userId, projectId);
        if (rule is null)
            return NotFound();

        return Ok(MapToView(rule));
    }

    /// <summary>Update a rate notification rule.</summary>
    [HttpPut("{ruleId:objectid}")]
    [Consumes("application/json")]
    public async Task<ActionResult<ViewRateNotificationRule>> PutAsync(
        string userId,
        string projectId,
        string ruleId,
        [FromBody] UpdateRateNotificationRule model)
    {
        if (!CanManage(userId))
            return NotFound();

        var project = await GetProjectAndCheckAccessAsync(projectId);
        if (project is null)
            return NotFound();

        var rule = await GetRuleAndCheckAccessAsync(ruleId, userId, projectId);
        if (rule is null)
            return NotFound();

        // Apply updates
        if (model.Name is not null)
            rule.Name = model.Name;

        if (model.Signal.HasValue)
            rule.Signal = model.Signal.Value;

        if (model.Subject.HasValue)
            rule.Subject = model.Subject.Value;

        if (model.Threshold.HasValue)
            rule.Threshold = model.Threshold.Value;

        // StackId update
        var newStackId = model.StackId;
        var newSubject = rule.Subject;

        if (newSubject == RateNotificationSubject.Stack)
        {
            if (String.IsNullOrEmpty(newStackId))
                return ValidationProblem(detail: "StackId is required when Subject is Stack.");

            var stack = await _stackRepository.GetByIdAsync(newStackId, o => o.Cache());
            if (stack is null || !String.Equals(stack.ProjectId, projectId, StringComparison.Ordinal))
                return ValidationProblem(detail: "The specified StackId does not belong to this project.");

            rule.StackId = newStackId;
        }
        else
        {
            rule.StackId = null;
        }

        if (model.Window.HasValue)
        {
            if (!ValidWindows.Contains(model.Window.Value))
                return ValidationProblem(detail: $"Window must be one of: {String.Join(", ", ValidWindows.Select(w => w.ToString()))}");
            rule.Window = model.Window.Value;
        }

        if (model.Cooldown.HasValue)
            rule.Cooldown = model.Cooldown.Value;

        if (rule.Cooldown < rule.Window)
            return ValidationProblem(detail: "Cooldown must be greater than or equal to Window.");

        if (model.IsEnabled.HasValue)
        {
            var org = await _organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache());
            rule.IsEnabled = model.IsEnabled.Value && (org?.HasPremiumFeatures ?? false);
        }

        rule.Version++;
        rule.UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime;

        await _ruleRepository.SaveAsync(rule, o => o.Cache());
        await _ruleCache.InvalidateAsync(projectId);

        return Ok(MapToView(rule));
    }

    /// <summary>Delete a rate notification rule.</summary>
    [HttpDelete("{ruleId:objectid}")]
    public async Task<IActionResult> DeleteAsync(string userId, string projectId, string ruleId)
    {
        if (!CanManage(userId))
            return NotFound();

        var rule = await GetRuleAndCheckAccessAsync(ruleId, userId, projectId);
        if (rule is null)
            return NotFound();

        await _ruleRepository.RemoveAsync(rule);
        await _ruleCache.InvalidateAsync(projectId);

        return NoContent();
    }

    /// <summary>Snooze a rate notification rule.</summary>
    [HttpPost("{ruleId:objectid}/snooze")]
    [Consumes("application/json")]
    public async Task<ActionResult<ViewRateNotificationRule>> SnoozeAsync(
        string userId,
        string projectId,
        string ruleId,
        [FromBody] SnoozeRateNotificationRuleRequest request)
    {
        if (!CanManage(userId))
            return NotFound();

        var rule = await GetRuleAndCheckAccessAsync(ruleId, userId, projectId);
        if (rule is null)
            return NotFound();

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (request.UntilUtc.HasValue)
        {
            rule.SnoozedUntilUtc = request.UntilUtc.Value;
        }
        else if (request.DurationSeconds.HasValue)
        {
            rule.SnoozedUntilUtc = now.AddSeconds(request.DurationSeconds.Value);
        }
        else
        {
            return ValidationProblem(detail: "Either DurationSeconds or UntilUtc must be provided.");
        }

        rule.Version++;
        rule.UpdatedUtc = now;
        await _ruleRepository.SaveAsync(rule, o => o.Cache());
        await _ruleCache.InvalidateAsync(projectId);

        return Ok(MapToView(rule));
    }

    /// <summary>Unsnooze a rate notification rule. Sets SnoozedUntilUtc = now to establish a fresh baseline.</summary>
    [HttpPost("{ruleId:objectid}/unsnooze")]
    public async Task<ActionResult<ViewRateNotificationRule>> UnsnoozeAsync(string userId, string projectId, string ruleId)
    {
        if (!CanManage(userId))
            return NotFound();

        var rule = await GetRuleAndCheckAccessAsync(ruleId, userId, projectId);
        if (rule is null)
            return NotFound();

        // Set to now (NOT null) so the evaluator uses now as the effective window start — no back-alert
        rule.SnoozedUntilUtc = _timeProvider.GetUtcNow().UtcDateTime;
        rule.Version++;
        rule.UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await _ruleRepository.SaveAsync(rule, o => o.Cache());
        await _ruleCache.InvalidateAsync(projectId);

        return Ok(MapToView(rule));
    }

    // ---- Helpers ----

    private bool CanManage(string userId)
    {
        // User can manage their own rules; global admins can manage any user's rules
        return String.Equals(CurrentUser.Id, userId, StringComparison.Ordinal) || Request.IsGlobalAdmin();
    }

    private async Task<Project?> GetProjectAndCheckAccessAsync(string projectId)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, o => o.Cache());
        if (project is null || !CanAccessOrganization(project.OrganizationId))
            return null;
        return project;
    }

    private async Task<RateNotificationRule?> GetRuleAndCheckAccessAsync(string ruleId, string userId, string projectId)
    {
        var rule = await _ruleRepository.GetByIdAsync(ruleId);
        if (rule is null)
            return null;

        // Rule must belong to the specified user+project
        if (!String.Equals(rule.UserId, userId, StringComparison.Ordinal))
            return null;

        if (!String.Equals(rule.ProjectId, projectId, StringComparison.Ordinal))
            return null;

        // Current user must be able to manage this rule
        if (!CanManage(userId))
            return null;

        return rule;
    }

    private ViewRateNotificationRule MapToView(RateNotificationRule rule)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return new ViewRateNotificationRule
        {
            Id = rule.Id,
            OrganizationId = rule.OrganizationId,
            ProjectId = rule.ProjectId,
            UserId = rule.UserId,
            Version = rule.Version,
            Name = rule.Name,
            IsEnabled = rule.IsEnabled,
            Signal = rule.Signal,
            Subject = rule.Subject,
            StackId = rule.StackId,
            Threshold = rule.Threshold,
            Window = rule.Window,
            Cooldown = rule.Cooldown,
            SnoozedUntilUtc = rule.SnoozedUntilUtc,
            IsSnoozed = rule.SnoozedUntilUtc.HasValue && rule.SnoozedUntilUtc.Value > now,
            LastFiredUtc = rule.LastFiredUtc,
            CreatedUtc = rule.CreatedUtc,
            UpdatedUtc = rule.UpdatedUtc
        };
    }
}
