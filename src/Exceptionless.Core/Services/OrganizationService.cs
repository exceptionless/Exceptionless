using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Exceptionless.Core.Services;

public class OrganizationService : IStartupAction
{
    private const int BATCH_SIZE = 50;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ITokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IWebHookRepository _webHookRepository;
    private readonly AppOptions _appOptions;
    private readonly UsageService _usageService;
    private readonly ILogger _logger;

    public OrganizationService(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, ITokenRepository tokenRepository, IUserRepository userRepository, IWebHookRepository webHookRepository, AppOptions appOptions, UsageService usageService, ILoggerFactory loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
        _webHookRepository = webHookRepository;
        _appOptions = appOptions;
        _usageService = usageService;
        _logger = loggerFactory.CreateLogger<OrganizationService>();
    }

    public Task RunAsync(CancellationToken shutdownToken = default)
    {
        _organizationRepository.DocumentsSaved.AddHandler(OnOrganizationSavedAsync);
        return Task.CompletedTask;
    }

    private async Task OnOrganizationSavedAsync(object source, ModifiedDocumentsEventArgs<Organization> args)
    {
        foreach (var doc in args.Documents)
        {
            if (doc.Original is null)
                continue;

            await _usageService.HandleOrganizationChangeAsync(doc.Value, doc.Original);

            if (!doc.Original.IsSuspended && doc.Value.IsSuspended)
                await _tokenRepository.PatchAllAsync(q => q.Organization(doc.Value.Id).FieldEquals(t => t.IsSuspended, false), new PartialPatch(new { is_suspended = true }), o => o.ImmediateConsistency());
            else if (doc.Original.IsSuspended && !doc.Value.IsSuspended)
                await _tokenRepository.PatchAllAsync(q => q.Organization(doc.Value.Id).FieldEquals(t => t.IsSuspended, true), new PartialPatch(new { is_suspended = false }), o => o.ImmediateConsistency());
        }
    }

    public async Task CancelSubscriptionsAsync(Organization organization)
    {
        if (String.IsNullOrEmpty(organization.StripeCustomerId))
            return;

        var client = new StripeClient(_appOptions.StripeOptions.StripeApiKey);
        var subscriptionService = new SubscriptionService(client);
        var subscriptions = await subscriptionService.ListAsync(new SubscriptionListOptions { Customer = organization.StripeCustomerId });
        foreach (var subscription in subscriptions.Where(s => !s.CanceledAt.HasValue))
        {
            _logger.LogInformation("Canceling stripe subscription ({SubscriptionId}) for {OrganizationName} ({Organization})", subscription.Id, organization.Name, organization.Id);
            await subscriptionService.CancelAsync(subscription.Id, new SubscriptionCancelOptions());
            _logger.LogInformation("Canceled stripe subscription ({SubscriptionId}) for {OrganizationName} ({Organization})", subscription.Id, organization.Name, organization.Id);
        }
    }

    public async Task<long> RemoveUsersAsync(Organization organization, string? currentUserId)
    {
        long totalUsersAffected = 0;
        var userResults = await _userRepository.GetByOrganizationIdAsync(organization.Id, o => o.SearchAfterPaging().PageLimit(BATCH_SIZE));

        while (userResults.Documents.Count > 0)
        {
            var usersToDelete = new List<User>(userResults.Documents.Count);
            var usersToUpdate = new List<User>(userResults.Documents.Count);

            foreach (var user in userResults.Documents)
            {
                // delete the user if they are not associated to any other organizations and they are not the current user
                if (user.OrganizationIds.All(oid => String.Equals(oid, organization.Id)) && !String.Equals(user.Id, currentUserId))
                {
                    _logger.LogInformation("Removing user {User} as they do not belong to any other organizations", user.Id);
                    usersToDelete.Add(user);
                }
                else
                {
                    _logger.LogInformation("Removing user {User} from organization: {OrganizationName} ({Organization})", user.Id, organization.Name, organization.Id);
                    user.OrganizationIds.Remove(organization.Id);
                    usersToUpdate.Add(user);
                }
            }

            if (usersToDelete.Count > 0)
                await _userRepository.RemoveAsync(usersToDelete);

            if (usersToUpdate.Count > 0)
                await _userRepository.SaveAsync(usersToUpdate, o => o.Cache());

            totalUsersAffected += usersToDelete.Count + usersToUpdate.Count;

            if (!await userResults.NextPageAsync())
                break;
        }

        return totalUsersAffected;
    }

    public Task<long> CleanupProjectNotificationSettingsAsync(Organization organization, IReadOnlyCollection<string> userIdsToRemove, CancellationToken cancellationToken = default, Func<Task>? renewWorkItemLockAsync = null)
    {
        ArgumentNullException.ThrowIfNull(organization);
        ArgumentNullException.ThrowIfNull(userIdsToRemove);

        return CleanupProjectNotificationSettingsAsync(organization.Id, userIdsToRemove, cancellationToken, renewWorkItemLockAsync);
    }

    private async Task<long> CleanupProjectNotificationSettingsAsync(string organizationId, IReadOnlyCollection<string> userIdsToRemove, CancellationToken cancellationToken, Func<Task>? renewWorkItemLockAsync)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        ArgumentNullException.ThrowIfNull(userIdsToRemove);

        using var _ = _logger.BeginScope(new ExceptionlessState().Organization(organizationId));

        var userIdsToRemoveSet = userIdsToRemove.Count is 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(userIdsToRemove, StringComparer.Ordinal);

        long removed = 0;
        var projectResults = await _projectRepository.GetByOrganizationIdAsync(organizationId, o => o.SearchAfterPaging().PageLimit(BATCH_SIZE));
        while (projectResults.Documents.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            var candidateUserIds = projectResults.Documents
                .SelectMany(project => project.NotificationSettings.Keys)
                .Where(key => !IsNotificationIntegrationKey(key))
                .ToHashSet(StringComparer.Ordinal);

            var validUserIds = await GetValidNotificationUserIdsAsync(organizationId, candidateUserIds, cancellationToken);
            var projectsToSave = new List<Project>(projectResults.Documents.Count);

            foreach (var project in projectResults.Documents)
            {
                int removedFromProject = RemoveInvalidNotificationSettings(project, validUserIds, userIdsToRemoveSet);
                if (removedFromProject <= 0)
                    continue;

                removed += removedFromProject;
                projectsToSave.Add(project);
            }

            if (projectsToSave.Count > 0)
                await _projectRepository.SaveAsync(projectsToSave);

            if (renewWorkItemLockAsync is not null)
                await renewWorkItemLockAsync();

            if (!await projectResults.NextPageAsync())
                break;
        }

        if (removed > 0)
            _logger.LogInformation("Removed {Count} invalid notification settings", removed);

        return removed;
    }

    public Task<long> RemoveTokensAsync(Organization organization)
    {
        _logger.LogInformation("Removing tokens for {OrganizationName} ({Organization})", organization.Name, organization.Id);
        return _tokenRepository.RemoveAllByOrganizationIdAsync(organization.Id);
    }

    public Task<long> RemoveWebHooksAsync(Organization organization)
    {
        _logger.LogInformation("Removing web hooks for {OrganizationName} ({Organization})", organization.Name, organization.Id);
        return _webHookRepository.RemoveAllByOrganizationIdAsync(organization.Id);
    }

    public async Task SoftDeleteOrganizationAsync(Organization organization, string currentUserId)
    {
        if (organization.IsDeleted)
            return;

        await RemoveTokensAsync(organization);
        await RemoveWebHooksAsync(organization);
        await CancelSubscriptionsAsync(organization);
        await RemoveUsersAsync(organization, currentUserId);
        await CleanupProjectNotificationSettingsAsync(organization, []);

        organization.IsDeleted = true;
        await _organizationRepository.SaveAsync(organization);
    }

    private async Task<HashSet<string>> GetValidNotificationUserIdsAsync(string organizationId, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
    {
        var validUserIds = new HashSet<string>(StringComparer.Ordinal);
        if (userIds.Count == 0)
            return validUserIds;

        foreach (string[] batch in userIds.Chunk(BATCH_SIZE))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var users = await _userRepository.GetByIdsAsync(batch);
            validUserIds.UnionWith(users.Where(user => user.OrganizationIds.Contains(organizationId)).Select(user => user.Id));
        }

        return validUserIds;
    }

    private static int RemoveInvalidNotificationSettings(Project project, IReadOnlySet<string> validUserIds, IReadOnlySet<string> userIdsToRemove)
    {
        var keysToRemove = project.NotificationSettings.Keys
            .Where(key => !IsNotificationIntegrationKey(key) && (userIdsToRemove.Contains(key) || !validUserIds.Contains(key)))
            .ToList();

        foreach (var key in keysToRemove)
            project.NotificationSettings.Remove(key);

        return keysToRemove.Count;
    }

    private static bool IsNotificationIntegrationKey(string key) =>
        String.Equals(key, Project.NotificationIntegrations.Slack, StringComparison.OrdinalIgnoreCase);
}
