using System;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
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
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Storage;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/admin")]
    [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class AdminController : ExceptionlessApiController {
        private readonly ExceptionlessElasticConfiguration _configuration;
        private readonly IFileStorage _fileStorage;
        private readonly IMessagePublisher _messagePublisher;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IQueue<EventPost> _eventPostQueue;
        private readonly IQueue<WorkItemData> _workItemQueue;

        public AdminController(ExceptionlessElasticConfiguration configuration, IFileStorage fileStorage, IMessagePublisher messagePublisher, IOrganizationRepository organizationRepository, IQueue<EventPost> eventPostQueue, IQueue<WorkItemData> workItemQueue) {
            _configuration = configuration;
            _fileStorage = fileStorage;
            _messagePublisher = messagePublisher;
            _organizationRepository = organizationRepository;
            _eventPostQueue = eventPostQueue;
            _workItemQueue = workItemQueue;
        }

        [HttpPost]
        [Route("change-plan")]
        public async Task<IHttpActionResult> ChangePlanAsync(string organizationId, string planId) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            if (organization == null)
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            var plan = BillingManager.GetBillingPlan(planId);
            if (plan == null)
                return Ok(new { Success = false, Message = "Invalid PlanId." });

            organization.BillingStatus = !String.Equals(plan.Id, BillingManager.FreePlan.Id) ? BillingStatus.Active : BillingStatus.Trialing;
            organization.RemoveSuspension();
            BillingManager.ApplyBillingPlan(organization, plan, CurrentUser, false);

            await _organizationRepository.SaveAsync(organization);
            await _messagePublisher.PublishAsync(new PlanChanged {
                OrganizationId = organization.Id
            });

            return Ok(new { Success = true });
        }

        [HttpPost]
        [Route("set-bonus")]
        public async Task<IHttpActionResult> SetBonusAsync(string organizationId, int bonusEvents, DateTime? expires = null) {
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

        [HttpGet]
        [Route("requeue")]
        public async Task<IHttpActionResult> RequeueAsync(string path = null, bool archive = false) {
            if (String.IsNullOrEmpty(path))
                path = @"q\*";

            foreach (var file in await _fileStorage.GetFileListAsync(path))
                await _eventPostQueue.EnqueueAsync(new EventPost { FilePath = file.Path, ShouldArchive = archive });

            return Ok();
        }

        [HttpGet]
        [Route("maintenance/{name:minlength(1)}")]
        public async Task<IHttpActionResult> RunJobAsync(string name) {
            switch (name.ToLowerInvariant()) {
                case "indexes":
                    if (!Settings.Current.DisableIndexConfiguration)
                        await _configuration.ConfigureIndexesAsync(beginReindexingOutdated: false);
                    break;
                case "update-organization-plans":
                    await _workItemQueue.EnqueueAsync(new OrganizationMaintenanceWorkItem { UpgradePlans = true });
                    break;
                case "update-project-default-bot-lists":
                    await _workItemQueue.EnqueueAsync(new ProjectMaintenanceWorkItem { UpdateDefaultBotList = true, IncrementConfigurationVersion = true });
                    break;
                case "increment-project-configuration-version":
                    await _workItemQueue.EnqueueAsync(new ProjectMaintenanceWorkItem { IncrementConfigurationVersion = true });
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
