using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Utility;
using Exceptionless.Web.Extensions;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using QueueOptions = Exceptionless.Core.Configuration.QueueOptions;

namespace Exceptionless.Web.Controllers {
    [Route(API_PREFIX + "/admin")]
    [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class AdminController : ExceptionlessApiController {
        private readonly ExceptionlessElasticConfiguration _configuration;
        private readonly IFileStorage _fileStorage;
        private readonly IMessagePublisher _messagePublisher;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IQueue<EventPost> _eventPostQueue;
        private readonly IQueue<WorkItemData> _workItemQueue;
        private readonly IOptions<AppOptions> _appOptions;
        private readonly IOptions<AuthOptions> _authOptions;
        private readonly IOptions<CacheOptions> _cacheOptions;
        private readonly IOptions<ElasticsearchOptions> _elasticsearchOptions;
        private readonly IOptions<EmailOptions> _emailOptions;
        private readonly IOptions<IntercomOptions> _intercomOptions;
        private readonly IOptions<MessageBusOptions> _messageBusOptions;
        private readonly IOptions<MetricOptions> _metricOptions;
        private readonly IOptions<QueueOptions> _queueOptions;
        private readonly IOptions<SlackOptions> _slackOptions;
        private readonly IOptions<StorageOptions> _storageOptions;
        private readonly IOptions<StripeOptions> _stripeOptions;
        private readonly BillingManager _billingManager;
        private readonly BillingPlans _plans;

        public AdminController(
            ExceptionlessElasticConfiguration configuration,
            IFileStorage fileStorage,
            IMessagePublisher messagePublisher,
            IOrganizationRepository organizationRepository,
            IQueue<EventPost> eventPostQueue,
            IQueue<WorkItemData> workItemQueue,
            IOptions<AppOptions> appOptions,
            IOptions<AuthOptions> authOptions,
            IOptions<CacheOptions> cacheOptions,
            IOptions<ElasticsearchOptions> elasticsearchOptions,
            IOptions<EmailOptions> emailOptions,
            IOptions<IntercomOptions> intercomOptions,
            IOptions<MessageBusOptions> messageBusOptions,
            IOptions<MetricOptions> metricOptions,
            IOptions<QueueOptions> queueOptions,
            IOptions<SlackOptions> slackOptions,
            IOptions<StorageOptions> storageOptions,
            IOptions<StripeOptions> stripeOptions,
            BillingManager billingManager,
            BillingPlans plans) {
            _configuration = configuration;
            _fileStorage = fileStorage;
            _messagePublisher = messagePublisher;
            _organizationRepository = organizationRepository;
            _eventPostQueue = eventPostQueue;
            _workItemQueue = workItemQueue;
            _appOptions = appOptions;
            _authOptions = authOptions;
            _cacheOptions = cacheOptions;
            _elasticsearchOptions = elasticsearchOptions;
            _emailOptions = emailOptions;
            _intercomOptions = intercomOptions;
            _messageBusOptions = messageBusOptions;
            _metricOptions = metricOptions;
            _queueOptions = queueOptions;
            _slackOptions = slackOptions;
            _storageOptions = storageOptions;
            _stripeOptions = stripeOptions;
            _billingManager = billingManager;
            _plans = plans;
        }

        [HttpGet("settings")]
        public ActionResult SettingsRequest() {
            return Ok(JsonConvert.SerializeObject(new {
                App = _appOptions.Value,
                Auth = _authOptions.Value,
                Cache = _cacheOptions.Value,
                Elasticsearch = _elasticsearchOptions.Value,
                Email = _emailOptions.Value,
                Intercom = _intercomOptions.Value,
                MessageBus = _messageBusOptions.Value,
                Metric = _metricOptions.Value,
                Queue = _queueOptions.Value,
                Slack = _slackOptions.Value,
                Storage = _storageOptions.Value,
                Stripe = _stripeOptions.Value
            }, Formatting.Indented));
        }

        
        [HttpGet("echo")]
        public ActionResult EchoRequest() {
            return Ok(JsonConvert.SerializeObject(new {
                Request.Headers,
                IpAddress = Request.GetClientIpAddress()
            }, Formatting.Indented));
        }
        
        [HttpGet("assemblies")]
        public ActionResult<IReadOnlyCollection<AssemblyDetail>> Assemblies() {
            var details = AssemblyDetail.ExtractAll();
            return Ok(details);
        }

        [HttpPost("change-plan")]
        public async Task<IActionResult> ChangePlanAsync(string organizationId, string planId) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            if (organization == null)
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            var plan = _billingManager.GetBillingPlan(planId);
            if (plan == null)
                return Ok(new { Success = false, Message = "Invalid PlanId." });

            organization.BillingStatus = !String.Equals(plan.Id, _plans.FreePlan.Id) ? BillingStatus.Active : BillingStatus.Trialing;
            organization.RemoveSuspension();
            _billingManager.ApplyBillingPlan(organization, plan, CurrentUser, false);

            await _organizationRepository.SaveAsync(organization, o => o.Cache());
            await _messagePublisher.PublishAsync(new PlanChanged {
                OrganizationId = organization.Id
            });

            return Ok(new { Success = true });
        }

        [HttpPost("set-bonus")]
        public async Task<IActionResult> SetBonusAsync(string organizationId, int bonusEvents, DateTime? expires = null) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            if (organization == null)
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            organization.BonusEventsPerMonth = bonusEvents;
            organization.BonusExpiration = expires;
            await _organizationRepository.SaveAsync(organization);

            return Ok(new { Success = true });
        }

        [HttpGet("requeue")]
        public async Task<IActionResult> RequeueAsync(string path = null, bool archive = false) {
            if (String.IsNullOrEmpty(path))
                path = @"q\*";

            int enqueued = 0;
            foreach (var file in await _fileStorage.GetFileListAsync(path)) {
                await _eventPostQueue.EnqueueAsync(new EventPost(_appOptions.Value.EnableArchive) { FilePath = file.Path, ShouldArchive = archive });
                enqueued++;
            }

            return Ok(new { Enqueued = enqueued });
        }

        [HttpGet("maintenance/{name:minlength(1)}")]
        public async Task<IActionResult> RunJobAsync(string name) {
            switch (name.ToLowerInvariant()) {
                case "indexes":
                    if (!_elasticsearchOptions.Value.DisableIndexConfiguration)
                        await _configuration.ConfigureIndexesAsync(beginReindexingOutdated: false);
                    break;
                case "update-organization-plans":
                    await _workItemQueue.EnqueueAsync(new OrganizationMaintenanceWorkItem { UpgradePlans = true });
                    break;
                case "remove-old-organization-usage":
                    await _workItemQueue.EnqueueAsync(new OrganizationMaintenanceWorkItem { RemoveOldUsageStats = true });
                    break;
                case "update-project-default-bot-lists":
                    await _workItemQueue.EnqueueAsync(new ProjectMaintenanceWorkItem { UpdateDefaultBotList = true, IncrementConfigurationVersion = true });
                    break;
                case "increment-project-configuration-version":
                    await _workItemQueue.EnqueueAsync(new ProjectMaintenanceWorkItem { IncrementConfigurationVersion = true });
                    break;
                case "remove-old-project-usage":
                    await _workItemQueue.EnqueueAsync(new ProjectMaintenanceWorkItem { RemoveOldUsageStats = true });
                    break;
                case "normalize-user-email-address":
                    await _workItemQueue.EnqueueAsync(new UserMaintenanceWorkItem { Normalize = true });
                    break;
                default:
                    return NotFound();
            }

            return Ok();
        }
    }
}
