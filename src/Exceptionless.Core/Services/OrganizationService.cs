using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
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
    private readonly StripeOptions _stripeOptions;
    private readonly UsageService _usageService;
    private readonly ILogger _logger;

    public OrganizationService(IOrganizationRepository organizationRepository, ITokenRepository tokenRepository, IUserRepository userRepository, IWebHookRepository webHookRepository, ICacheClient cache, StripeOptions stripeOptions, UsageService usageService, ILoggerFactory loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
        _webHookRepository = webHookRepository;
        _cache = cache;
        _stripeOptions = stripeOptions;
        _usageService = usageService;
        _logger = loggerFactory.CreateLogger<OrganizationService>();
    }

    public Task RunAsync(CancellationToken shutdownToken = default)
    {
        _organizationRepository.DocumentsSaved.AddHandler(OrgChanged);
        return Task.CompletedTask;
    }

    private async Task OrgChanged(object source, ModifiedDocumentsEventArgs<Organization> args)
    {
        foreach (var doc in args.Documents)
        {
            if (doc.Original != null)
            {
                await _usageService.HandleOrganizationChange(doc.Value, doc.Original);

                if (doc.Original.IsSuspended == false && doc.Value.IsSuspended == true)
                    await _tokenRepository.PatchAllAsync(q => q.Organization(doc.Value.Id).FieldEquals(t => t.IsSuspended, false), new PartialPatch(new { is_suspended = true }), o => o.ImmediateConsistency());
                else if (doc.Original.IsSuspended == true && doc.Value.IsSuspended == false)
                    await _tokenRepository.PatchAllAsync(q => q.Organization(doc.Value.Id).FieldEquals(t => t.IsSuspended, true), new PartialPatch(new { is_suspended = false }), o => o.ImmediateConsistency());
            }
        }
    }

    public async Task CancelSubscriptionsAsync(Organization organization)
    {
        if (String.IsNullOrEmpty(organization.StripeCustomerId))
            return;

        var client = new StripeClient(_stripeOptions.StripeApiKey);
        var subscriptionService = new SubscriptionService(client);
        var subscriptions = await subscriptionService.ListAsync(new SubscriptionListOptions { Customer = organization.StripeCustomerId }).AnyContext();
        foreach (var subscription in subscriptions.Where(s => !s.CanceledAt.HasValue))
        {
            _logger.LogInformation("Canceling stripe subscription ({SubscriptionId}) for {OrganizationName} ({organization})", subscription.Id, organization.Name, organization.Id);
            await subscriptionService.CancelAsync(subscription.Id, new SubscriptionCancelOptions()).AnyContext();
            _logger.LogInformation("Canceled stripe subscription ({SubscriptionId}) for {OrganizationName} ({organization})", subscription.Id, organization.Name, organization.Id);
        }
    }

    public async Task<long> RemoveUsersAsync(Organization organization, string currentUserId)
    {
        var users = await _userRepository.GetByOrganizationIdAsync(organization.Id, o => o.PageLimit(1000)).AnyContext();
        foreach (var user in users.Documents)
        {
            // delete the user if they are not associated to any other organizations and they are not the current user
            if (user.OrganizationIds.All(oid => String.Equals(oid, organization.Id)) && !String.Equals(user.Id, currentUserId))
            {
                _logger.LogInformation("Removing user {User} as they do not belong to any other organizations.", user.Id);
                await _userRepository.RemoveAsync(user.Id).AnyContext();
            }
            else
            {
                _logger.LogInformation("Removing user {User} from organization: {OrganizationName} ({OrganizationId})", user.Id, organization.Name, organization.Id);
                user.OrganizationIds.Remove(organization.Id);

                // Temp fix for old user records :\.
                if (!user.IsEmailAddressVerified && String.IsNullOrEmpty(user.VerifyEmailAddressToken))
                    user.CreateVerifyEmailAddressToken();

                await _userRepository.SaveAsync(user, o => o.Cache()).AnyContext();
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

        await RemoveTokensAsync(organization).AnyContext();
        await RemoveWebHooksAsync(organization).AnyContext();
        await CancelSubscriptionsAsync(organization).AnyContext();
        await RemoveUsersAsync(organization, currentUserId).AnyContext();

        organization.IsDeleted = true;
        await _organizationRepository.SaveAsync(organization).AnyContext();
    }
}
