using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Logging;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class EnqueueOrganizationNotificationOnPlanOverage {
        private readonly IQueue<WorkItemData> _workItemQueue;
        private readonly IMessageSubscriber _subscriber;
        private readonly ILogger _logger;

        public EnqueueOrganizationNotificationOnPlanOverage(IQueue<WorkItemData> workItemQueue, IMessageSubscriber subscriber, ILoggerFactory loggerFactory = null) {
            _workItemQueue = workItemQueue;
            _subscriber = subscriber;
            _logger = loggerFactory.CreateLogger<EnqueueOrganizationNotificationOnPlanOverage>();
        }

        public Task RunAsync(CancellationToken token) {
            return _subscriber.SubscribeAsync<PlanOverage>(async overage => {
                _logger.Info("Enqueueing plan overage work item for organization: {0} IsOverHourlyLimit: {1} IsOverMonthlyLimit: {2}", overage.OrganizationId, overage.IsHourly, !overage.IsHourly);
                await _workItemQueue.EnqueueAsync(new OrganizationNotificationWorkItem {
                    OrganizationId = overage.OrganizationId,
                    IsOverHourlyLimit = overage.IsHourly,
                    IsOverMonthlyLimit = !overage.IsHourly
                }).AnyContext();
            }, token);
        }
    }

    public class OrganizationNotificationWorkItemHandler : WorkItemHandlerBase {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMailer _mailer;
        private readonly ILockProvider _lockProvider;

        public OrganizationNotificationWorkItemHandler(IOrganizationRepository organizationRepository, IUserRepository userRepository, IMailer mailer, ICacheClient cacheClient, IMessageBus messageBus, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _mailer = mailer;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromHours(1));
        }

        public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new CancellationToken()) {
            string cacheKey = $"{nameof(OrganizationNotificationWorkItemHandler)}:{((OrganizationNotificationWorkItem)workItem).OrganizationId}";
            return _lockProvider.AcquireAsync(cacheKey, TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        public override async Task HandleItemAsync(WorkItemContext context) {
            var workItem = context.GetData<OrganizationNotificationWorkItem>();
            Log.Info("Received organization notification work item for: {0} IsOverHourlyLimit: {1} IsOverMonthlyLimit: {2}", workItem.OrganizationId, workItem.IsOverHourlyLimit, workItem.IsOverMonthlyLimit);

            var organization = await _organizationRepository.GetByIdAsync(workItem.OrganizationId, o => o.Cache()).AnyContext();
            if (organization == null)
                return;

            if (workItem.IsOverMonthlyLimit)
                await SendOverageNotificationsAsync(organization, workItem.IsOverHourlyLimit, workItem.IsOverMonthlyLimit).AnyContext();
        }

        private async Task SendOverageNotificationsAsync(Organization organization, bool isOverHourlyLimit, bool isOverMonthlyLimit) {
            var results = await _userRepository.GetByOrganizationIdAsync(organization.Id).AnyContext();
            foreach (var user in results.Documents) {
                if (!user.IsEmailAddressVerified) {
                    Log.Info("User {0} with email address {1} has not been verified.", user.Id, user.EmailAddress);
                    continue;
                }

                if (!user.EmailNotificationsEnabled) {
                    Log.Info().Message("User {0} with email address {1} has email notifications disabled.", user.Id, user.EmailAddress);
                    continue;
                }

                Log.Trace("Sending email to {0}...", user.EmailAddress);
                await _mailer.SendOrganizationNoticeAsync(user.EmailAddress, new OrganizationNotificationModel {
                    Organization = organization,
                    IsOverHourlyLimit = isOverHourlyLimit,
                    IsOverMonthlyLimit = isOverMonthlyLimit
                }).AnyContext();
            }

            Log.Trace().Message("Done sending email.");
        }
    }
}