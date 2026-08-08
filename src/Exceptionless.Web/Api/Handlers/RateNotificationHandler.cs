using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
using Foundatio.Lock;
using Foundatio.Mediator;
using Foundatio.Repositories;

namespace Exceptionless.Web.Api.Handlers;

public class RateNotificationHandler(
    IRateNotificationRuleRepository ruleRepository,
    IProjectRepository projectRepository,
    IOrganizationRepository organizationRepository,
    IUserRepository userRepository,
    IStackRepository stackRepository,
    ILockProvider lockProvider,
    TimeProvider timeProvider)
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

    public async Task<Result<PagedResult<ViewRateNotificationRule>>> Handle(GetRateNotifications message)
    {
        var project = await GetProjectAndCheckAccessAsync(message.ProjectId, message.UserId, message.Context);
        if (project is null)
            return Result.NotFound("Rate notification rules not found.");

        int page = Pagination.GetPage(message.Page);
        int limit = Pagination.GetLimit(message.Limit);
        var results = await ruleRepository.GetByProjectIdAndUserIdAsync(message.ProjectId, message.UserId, o => o.PageNumber(page).PageLimit(limit));
        return new PagedResult<ViewRateNotificationRule>(results.Documents.Select(MapToView).ToList(), results.HasMore && !Pagination.NextPageExceedsSkipLimit(page, limit), page, results.Total);
    }

    public async Task<Result<ViewRateNotificationRule>> Handle(CreateRateNotification message)
    {
        var project = await GetProjectAndCheckAccessAsync(message.ProjectId, message.UserId, message.Context);
        if (project is null)
            return Result.NotFound("Project not found.");

        var model = message.Rule;
        var validation = await ValidateRuleAsync(model.Name, model.Signal, model.Subject, model.StackId, model.Window, model.Cooldown, message.ProjectId, project.OrganizationId);
        if (validation is not null)
            return Result<ViewRateNotificationRule>.FromResult(validation);

        var organization = await organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache());
        bool isEnabled = model.IsEnabled && organization?.HasRateNotifications() == true;

        await using var createLock = await lockProvider.TryAcquireAsync($"rate-notification:create:{message.ProjectId}:{message.UserId}", TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(5));
        if (createLock is null)
            return Result.Conflict("Another rate notification rule is being created. Please retry.");

        long count = await ruleRepository.CountByProjectIdAndUserIdAsync(message.ProjectId, message.UserId);
        if (count >= MaxRulesPerUserPerProject)
            return Invalid<ViewRateNotificationRule>("general", $"Maximum of {MaxRulesPerUserPerProject} rate notification rules per user per project.");

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        var rule = await ruleRepository.AddAsync(new RateNotificationRule
        {
            OrganizationId = project.OrganizationId,
            ProjectId = message.ProjectId,
            UserId = message.UserId,
            Name = model.Name.Trim(),
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
        }, o => o.Cache().ImmediateConsistency());

        string location = $"/api/v2/users/{message.UserId}/projects/{message.ProjectId}/rate-notifications/{rule.Id}";
        return Result<ViewRateNotificationRule>.Created(MapToView(rule), location);
    }

    public async Task<Result<ViewRateNotificationRule>> Handle(GetRateNotificationById message)
    {
        var rule = await GetRuleAndCheckAccessAsync(message.UserId, message.ProjectId, message.RuleId, message.Context);
        return rule is null ? Result.NotFound("Rate notification rule not found.") : MapToView(rule);
    }

    public async Task<Result<ViewRateNotificationRule>> Handle(UpdateRateNotification message)
    {
        var project = await GetProjectAndCheckAccessAsync(message.ProjectId, message.UserId, message.Context);
        if (project is null)
            return Result.NotFound("Project not found.");

        await using var mutationLock = await TryAcquireRuleMutationLockAsync(message.RuleId);
        if (mutationLock is null)
            return Result.Conflict("Another update to this rate notification rule is in progress. Please retry.");

        var rule = await GetRuleAndCheckAccessAsync(message.UserId, project, message.RuleId);
        if (rule is null)
            return Result.NotFound("Rate notification rule not found.");

        var model = message.Rule;
        string name = model.Name ?? rule.Name;
        var signal = model.Signal ?? rule.Signal;
        var subject = model.Subject ?? rule.Subject;
        string? stackId = subject == RateNotificationSubject.Stack ? model.StackId ?? rule.StackId : null;
        TimeSpan window = model.Window ?? rule.Window;
        TimeSpan cooldown = model.Cooldown ?? rule.Cooldown;
        var validation = await ValidateRuleAsync(name, signal, subject, stackId, window, cooldown, message.ProjectId, project.OrganizationId);
        if (validation is not null)
            return Result<ViewRateNotificationRule>.FromResult(validation);

        rule.Name = name.Trim();
        rule.Signal = signal;
        rule.Subject = subject;
        rule.StackId = subject == RateNotificationSubject.Stack ? stackId : null;
        rule.Threshold = model.Threshold ?? rule.Threshold;
        rule.Window = window;
        rule.Cooldown = cooldown;
        if (model.IsEnabled.HasValue)
        {
            var organization = await organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache());
            rule.IsEnabled = model.IsEnabled.Value && organization?.HasRateNotifications() == true;
        }

        rule.Version++;
        rule.UpdatedUtc = timeProvider.GetUtcNow().UtcDateTime;
        await ruleRepository.SaveAsync(rule, o => o.Cache().ImmediateConsistency());
        return MapToView(rule);
    }

    public async Task<Result> Handle(DeleteRateNotification message)
    {
        var project = await GetProjectAndCheckAccessAsync(message.ProjectId, message.UserId, message.Context);
        if (project is null)
            return Result.NotFound("Project not found.");

        await using var mutationLock = await TryAcquireRuleMutationLockAsync(message.RuleId);
        if (mutationLock is null)
            return Result.Conflict("Another update to this rate notification rule is in progress. Please retry.");

        var rule = await GetRuleAndCheckAccessAsync(message.UserId, project, message.RuleId);
        if (rule is null)
            return Result.NotFound("Rate notification rule not found.");

        await ruleRepository.RemoveAsync(rule, o => o.ImmediateConsistency());
        return Result.NoContent();
    }

    public async Task<Result<ViewRateNotificationRule>> Handle(SnoozeRateNotification message)
    {
        var project = await GetProjectAndCheckAccessAsync(message.ProjectId, message.UserId, message.Context);
        if (project is null)
            return Result.NotFound("Project not found.");

        await using var mutationLock = await TryAcquireRuleMutationLockAsync(message.RuleId);
        if (mutationLock is null)
            return Result.Conflict("Another update to this rate notification rule is in progress. Please retry.");

        var rule = await GetRuleAndCheckAccessAsync(message.UserId, project, message.RuleId);
        if (rule is null)
            return Result.NotFound("Rate notification rule not found.");

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        if (message.Request.UntilUtc.HasValue == message.Request.DurationSeconds.HasValue)
            return Invalid<ViewRateNotificationRule>("general", "Either DurationSeconds or UntilUtc must be provided.");

        DateTime snoozedUntilUtc = message.Request.UntilUtc ?? now.AddSeconds(message.Request.DurationSeconds!.Value);
        if (snoozedUntilUtc <= now)
            return Invalid<ViewRateNotificationRule>("general", "Snooze expiration must be in the future.");

        rule.SnoozedUntilUtc = snoozedUntilUtc;
        await SaveMutationAsync(rule, now);
        return MapToView(rule);
    }

    public async Task<Result<ViewRateNotificationRule>> Handle(UnsnoozeRateNotification message)
    {
        var project = await GetProjectAndCheckAccessAsync(message.ProjectId, message.UserId, message.Context);
        if (project is null)
            return Result.NotFound("Project not found.");

        await using var mutationLock = await TryAcquireRuleMutationLockAsync(message.RuleId);
        if (mutationLock is null)
            return Result.Conflict("Another update to this rate notification rule is in progress. Please retry.");

        var rule = await GetRuleAndCheckAccessAsync(message.UserId, project, message.RuleId);
        if (rule is null)
            return Result.NotFound("Rate notification rule not found.");

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        rule.SnoozedUntilUtc = now;
        await SaveMutationAsync(rule, now);
        return MapToView(rule);
    }

    private async Task<Result?> ValidateRuleAsync(string name, RateNotificationSignal signal, RateNotificationSubject subject, string? stackId, TimeSpan window, TimeSpan cooldown, string projectId, string organizationId)
    {
        if (String.IsNullOrWhiteSpace(name))
            return Result.Invalid(ValidationError.Create("name", "Name is required."));
        if (!Enum.IsDefined(signal))
            return Result.Invalid(ValidationError.Create("signal", "Signal is invalid."));
        if (!Enum.IsDefined(subject))
            return Result.Invalid(ValidationError.Create("subject", "Subject is invalid."));
        if (!ValidWindows.Contains(window))
            return Result.Invalid(ValidationError.Create("window", $"Window must be one of: {String.Join(", ", ValidWindows.Select(value => value.ToString()))}"));
        if (cooldown < window)
            return Result.Invalid(ValidationError.Create("cooldown", "Cooldown must be greater than or equal to Window."));
        if (cooldown > RateNotificationRule.MaximumCooldown)
            return Result.Invalid(ValidationError.Create("cooldown", "Cooldown must not exceed 24 hours."));
        if (subject == RateNotificationSubject.Project)
            return String.IsNullOrEmpty(stackId) ? null : Result.Invalid(ValidationError.Create("stack_id", "StackId must be empty when Subject is Project."));
        if (String.IsNullOrEmpty(stackId))
            return Result.Invalid(ValidationError.Create("stack_id", "StackId is required when Subject is Stack."));

        var stack = await stackRepository.GetByIdAsync(stackId, o => o.Cache());
        return stack is null || !String.Equals(stack.ProjectId, projectId, StringComparison.Ordinal) || !String.Equals(stack.OrganizationId, organizationId, StringComparison.Ordinal)
            ? Result.Invalid(ValidationError.Create("stack_id", "The specified StackId does not belong to this project."))
            : null;
    }

    private async Task<Project?> GetProjectAndCheckAccessAsync(string projectId, string userId, HttpContext context)
    {
        if (!CanManage(userId, context))
            return null;
        var project = await projectRepository.GetByIdAsync(projectId, o => o.Cache());
        if (project is null || !context.Request.CanAccessOrganization(project.OrganizationId))
            return null;
        var user = await userRepository.GetByIdAsync(userId, o => o.Cache());
        return user?.OrganizationIds.Contains(project.OrganizationId) == true ? project : null;
    }

    private async Task<RateNotificationRule?> GetRuleAndCheckAccessAsync(string userId, string projectId, string ruleId, HttpContext context)
    {
        var project = await GetProjectAndCheckAccessAsync(projectId, userId, context);
        return project is null ? null : await GetRuleAndCheckAccessAsync(userId, project, ruleId);
    }

    private async Task<RateNotificationRule?> GetRuleAndCheckAccessAsync(string userId, Project project, string ruleId)
    {
        var rule = await ruleRepository.GetByIdAsync(ruleId);
        return rule is not null && String.Equals(rule.UserId, userId, StringComparison.Ordinal) && String.Equals(rule.ProjectId, project.Id, StringComparison.Ordinal) && String.Equals(rule.OrganizationId, project.OrganizationId, StringComparison.Ordinal) ? rule : null;
    }

    private static bool CanManage(string userId, HttpContext context) => String.Equals(context.Request.GetUser().Id, userId, StringComparison.Ordinal) || context.Request.IsGlobalAdmin();
    private Task<ILock?> TryAcquireRuleMutationLockAsync(string ruleId) => lockProvider.TryAcquireAsync($"rate-notification:mutation:{ruleId}", TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(5));

    private Task SaveMutationAsync(RateNotificationRule rule, DateTime now)
    {
        rule.Version++;
        rule.UpdatedUtc = now;
        return ruleRepository.SaveAsync(rule, o => o.Cache().ImmediateConsistency());
    }

    private ViewRateNotificationRule MapToView(RateNotificationRule rule) => new()
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
        IsSnoozed = rule.SnoozedUntilUtc.HasValue && rule.SnoozedUntilUtc.Value > timeProvider.GetUtcNow().UtcDateTime,
        LastFiredUtc = rule.LastFiredUtc,
        CreatedUtc = rule.CreatedUtc,
        UpdatedUtc = rule.UpdatedUtc
    };

    private static Result<T> Invalid<T>(string identifier, string message) => Result<T>.FromResult(Result.Invalid(ValidationError.Create(identifier, message)));
}
