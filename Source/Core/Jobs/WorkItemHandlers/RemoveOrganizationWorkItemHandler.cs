using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Logging;
using Foundatio.Messaging;
using Stripe;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class RemoveOrganizationWorkItemHandler : WorkItemHandlerBase {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IUserRepository _userRepository;
        private readonly ITokenRepository _tokenRepository;
        private readonly IWebHookRepository _webHookRepository;
        private readonly ILockProvider _lockProvider;

        public RemoveOrganizationWorkItemHandler(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IEventRepository eventRepository, IStackRepository stackRepository, ITokenRepository tokenRepository, IUserRepository userRepository, IWebHookRepository webHookRepository, ICacheClient cacheClient, IMessageBus messageBus, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _eventRepository = eventRepository;
            _stackRepository = stackRepository;
            _tokenRepository = tokenRepository;
            _userRepository = userRepository;
            _webHookRepository = webHookRepository;
            _lockProvider = new CacheLockProvider(cacheClient, messageBus);
        }

        public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new CancellationToken()) {
            var cacheKey = $"{nameof(RemoveOrganizationWorkItemHandler)}:{((RemoveOrganizationWorkItem)workItem).OrganizationId}";
            return _lockProvider.AcquireAsync(cacheKey, TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        public override async Task HandleItemAsync(WorkItemContext context) {
            var workItem = context.GetData<RemoveOrganizationWorkItem>();
            _logger.Info("Received remove organization work item for: {0}", workItem.OrganizationId);

            await context.ReportProgressAsync(0, "Starting deletion...").AnyContext();
            var organization = await _organizationRepository.GetByIdAsync(workItem.OrganizationId).AnyContext();
            if (organization == null) {
                await context.ReportProgressAsync(100, "Organization deleted").AnyContext();
                return;
            }

            await context.ReportProgressAsync(10, "Removing subscriptions").AnyContext();
            if (!String.IsNullOrEmpty(organization.StripeCustomerId)) {
                _logger.Info("Canceling stripe subscription for the organization '{0}' with Id: '{1}'.", organization.Name, organization.Id);

                var subscriptionService = new StripeSubscriptionService(Settings.Current.StripeApiKey);
                var subscriptions = subscriptionService.List(organization.StripeCustomerId).Where(s => !s.CanceledAt.HasValue);
                foreach (var subscription in subscriptions)
                    subscriptionService.Cancel(organization.StripeCustomerId, subscription.Id);
            }

            await context.ReportProgressAsync(20, "Removing users").AnyContext();
            var users = await _userRepository.GetByOrganizationIdAsync(organization.Id).AnyContext();
            foreach (User user in users.Documents) {
                // delete the user if they are not associated to any other organizations and they are not the current user
                if (user.OrganizationIds.All(oid => String.Equals(oid, organization.Id)) && !String.Equals(user.Id, workItem.CurrentUserId)) {
                    _logger.Info("Removing user '{0}' as they do not belong to any other organizations.", user.Id, organization.Name, organization.Id);
                    await _userRepository.RemoveAsync(user.Id).AnyContext();
                } else {
                    _logger.Info("Removing user '{0}' from organization '{1}' with Id: '{2}'", user.Id, organization.Name, organization.Id);
                    user.OrganizationIds.Remove(organization.Id);
                    await _userRepository.SaveAsync(user, true).AnyContext();
                }
            }

            await context.ReportProgressAsync(30, "Removing tokens").AnyContext();
            await _tokenRepository.RemoveAllByOrganizationIdsAsync(new[] { organization.Id }).AnyContext();

            await context.ReportProgressAsync(40, "Removing web hooks").AnyContext();
            await _webHookRepository.RemoveAllByOrganizationIdsAsync(new[] { organization.Id }).AnyContext();

            await context.ReportProgressAsync(50, "Removing projects").AnyContext();
            var projects = await _projectRepository.GetByOrganizationIdAsync(organization.Id).AnyContext();
            if (workItem.IsGlobalAdmin && projects.Total > 0) {
                var completed = 1;
                foreach (Project project in projects.Documents) {
                    _logger.Info("Resetting all project data for project '{0}' with Id: '{1}'.", project.Name, project.Id);
                    await _eventRepository.RemoveAllByProjectIdsAsync(new[] { project.Id }).AnyContext();
                    await _stackRepository.RemoveAllByProjectIdsAsync(new[] { project.Id }).AnyContext();
                    await context.ReportProgressAsync(CalculateProgress(projects.Total, completed++, 51, 89), "Removing projects...").AnyContext();
                }

                _logger.Info("Deleting all projects for organization '{0}' with Id: '{1}'.", organization.Name, organization.Id);
                await _projectRepository.RemoveAsync(projects.Documents).AnyContext();
            }

            _logger.Info("Deleting organization '{0}' with Id: '{1}'.", organization.Name, organization.Id);
            await context.ReportProgressAsync(90, "Removing organization").AnyContext();
            await _organizationRepository.RemoveAsync(organization.Id).AnyContext();

            await context.ReportProgressAsync(100, "Organization deleted").AnyContext();
        }
    }
}
