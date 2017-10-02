using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class EnqueueOrganizationNotificationOnPlanOverage : IStartupAction {
        private readonly IQueue<WorkItemData> _workItemQueue;
        private readonly IMessageSubscriber _subscriber;
        private readonly ILogger _logger;

        public EnqueueOrganizationNotificationOnPlanOverage(IQueue<WorkItemData> workItemQueue, IMessageSubscriber subscriber, ILoggerFactory loggerFactory = null) {
            _workItemQueue = workItemQueue;
            _subscriber = subscriber;
            _logger = loggerFactory.CreateLogger<EnqueueOrganizationNotificationOnPlanOverage>();
        }

        public Task RunAsync(CancellationToken token) {
            return _subscriber.SubscribeAsync<PlanOverage>(overage => {
                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("Enqueueing plan overage work item for organization: {OrganizationId} IsOverHourlyLimit: {IsOverHourlyLimit} IsOverMonthlyLimit: {IsOverMonthlyLimit}", overage.OrganizationId, overage.IsHourly, !overage.IsHourly);

                return _workItemQueue.EnqueueAsync(new OrganizationNotificationWorkItem {
                    OrganizationId = overage.OrganizationId,
                    IsOverHourlyLimit = overage.IsHourly,
                    IsOverMonthlyLimit = !overage.IsHourly
                });
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
            Log.LogInformation("Received organization notification work item for: {organization} IsOverHourlyLimit: {IsOverHourlyLimit} IsOverMonthlyLimit: {IsOverMonthlyLimit}", workItem.OrganizationId, workItem.IsOverHourlyLimit, workItem.IsOverMonthlyLimit);

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
                    Log.LogInformation("User {user} with email address {EmailAddress} has not been verified.", user.Id, user.EmailAddress);
                    continue;
                }

                if (!user.EmailNotificationsEnabled) {
                    Log.LogInformation("User {user} with email address {EmailAddress} has email notifications disabled.", user.Id, user.EmailAddress);
                    continue;
                }

                Log.LogTrace("Sending email to {EmailAddress}...", user.EmailAddress);
                await _mailer.SendOrganizationNoticeAsync(user, organization, isOverMonthlyLimit, isOverHourlyLimit).AnyContext();
            }

            Log.LogTrace("Done sending email.");
        }
    }
}