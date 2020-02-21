using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
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
        private readonly AppOptions _appOptions;
        private readonly BillingManager _billingManager;
        private readonly BillingPlans _plans;

        public AdminController(
            ExceptionlessElasticConfiguration configuration,
            IFileStorage fileStorage,
            IMessagePublisher messagePublisher,
            IOrganizationRepository organizationRepository,
            IQueue<EventPost> eventPostQueue,
            IQueue<WorkItemData> workItemQueue,
            AppOptions appOptions,
            BillingManager billingManager,
            BillingPlans plans) {
            _configuration = configuration;
            _fileStorage = fileStorage;
            _messagePublisher = messagePublisher;
            _organizationRepository = organizationRepository;
            _eventPostQueue = eventPostQueue;
            _workItemQueue = workItemQueue;
            _appOptions = appOptions;
            _billingManager = billingManager;
            _plans = plans;
        }

        [HttpGet("settings")]
        public ActionResult SettingsRequest() {
            return Ok(_appOptions);
        }

        [HttpGet("echo")]
        public ActionResult EchoRequest() {
            return Ok(new {
                Request.Headers,
                IpAddress = Request.GetClientIpAddress()
            });
        }
        
        [HttpGet("assemblies")]
        public ActionResult<IReadOnlyCollection<AssemblyDetail>> Assemblies() {
            var details = AssemblyDetail.ExtractAll();
            return Ok(details);
        }

        [Consumes("application/json")]
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

        [Consumes("application/json")]
        [HttpPost("set-bonus")]
        public async Task<IActionResult> SetBonusAsync(string organizationId, int bonusEvents, DateTime? expires = null) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            if (organization == null)
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            _billingManager.ApplyBonus(organization, bonusEvents, expires);
            await _organizationRepository.SaveAsync(organization, o => o.Cache());

            return Ok(new { Success = true });
        }

        [HttpGet("requeue")]
        public async Task<IActionResult> RequeueAsync(string path = null, bool archive = false) {
            if (String.IsNullOrEmpty(path))
                path = @"q\*";

            int enqueued = 0;
            foreach (var file in await _fileStorage.GetFileListAsync(path)) {
                await _eventPostQueue.EnqueueAsync(new EventPost(_appOptions.EnableArchive) { FilePath = file.Path, ShouldArchive = archive });
                enqueued++;
            }

            return Ok(new { Enqueued = enqueued });
        }

        [HttpGet("maintenance/{name:minlength(1)}")]
        public async Task<IActionResult> RunJobAsync(string name) {
            switch (name.ToLowerInvariant()) {
                case "indexes":
                    if (!_appOptions.ElasticsearchOptions.DisableIndexConfiguration)
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
