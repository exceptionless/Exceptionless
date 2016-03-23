using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models.WorkItems;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Messaging;
using Foundatio.Queues;

namespace Exceptionless.Core.Jobs {
    public class MessageBusBrokerJob : JobBase {
        private readonly IQueue<WorkItemData> _workItemQueue;
        private readonly IMessageSubscriber _subscriber;

        public MessageBusBrokerJob(IQueue<WorkItemData> workItemQueue, IMessageSubscriber subscriber, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _workItemQueue = workItemQueue;
            _subscriber = subscriber;
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            _subscriber.Subscribe<PlanOverage>(OnPlanOverageAsync, context.CancellationToken);

            while (!context.CancellationToken.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromSeconds(5));

            return JobResult.Success;
        }

        private async Task OnPlanOverageAsync(PlanOverage overage, CancellationToken cancellationToken = default(CancellationToken)) {
            if (overage == null)
                return;

            _logger.Info("Enqueueing plan overage work item for organization: {0} IsOverHourlyLimit: {1} IsOverMonthlyLimit: {2}", overage.OrganizationId, overage.IsHourly, !overage.IsHourly);
            await _workItemQueue.EnqueueAsync(new OrganizationNotificationWorkItem {
                OrganizationId = overage.OrganizationId,
                IsOverHourlyLimit = overage.IsHourly,
                IsOverMonthlyLimit = !overage.IsHourly
            }).AnyContext();
        }
    }
}
