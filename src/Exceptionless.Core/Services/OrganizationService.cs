using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Exceptionless.Core.Services;

public class OrganizationService : IStartupAction
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IWebHookRepository _webHookRepository;
    private readonly ICacheClient _cache;
    private readonly AppOptions _appOptions;
    private readonly UsageService _usageService;
    private readonly ILogger _logger;

    public OrganizationService(IOrganizationRepository organizationRepository, ITokenRepository tokenRepository, IUserRepository userRepository, IWebHookRepository webHookRepository, ICacheClient cache, AppOptions appOptions, UsageService usageService, ILoggerFactory loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
        _webHookRepository = webHookRepository;
        _cache = cache;
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
        var users = await _userRepository.GetByOrganizationIdAsync(organization.Id, o => o.PageLimit(1000));
        foreach (var user in users.Documents)
        {
            // delete the user if they are not associated to any other organizations and they are not the current user
            if (user.OrganizationIds.All(oid => String.Equals(oid, organization.Id)) && !String.Equals(user.Id, currentUserId))
            {
                _logger.LogInformation("Removing user {User} as they do not belong to any other organizations", user.Id);
                await _userRepository.RemoveAsync(user.Id);
            }
            else
            {
                _logger.LogInformation("Removing user {User} from organization: {OrganizationName} ({OrganizationId})", user.Id, organization.Name, organization.Id);
                user.OrganizationIds.Remove(organization.Id);

                await _userRepository.SaveAsync(user, o => o.Cache());
            }
        }

        return users.Documents.Count;
    }

    public Task<long> RemoveTokensAsync(Organization organization)
    {
        _logger.LogInformation("Removing tokens for {OrganizationName} ({OrganizationId})", organization.Name, organization.Id);
        return _tokenRepository.RemoveAllByOrganizationIdAsync(organization.Id);
    }

    public Task<long> RemoveWebHooksAsync(Organization organization)
    {
        _logger.LogInformation("Removing web hooks for {OrganizationName} ({OrganizationId})", organization.Name, organization.Id);
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

        organization.IsDeleted = true;
        await _organizationRepository.SaveAsync(organization);
    }
}
