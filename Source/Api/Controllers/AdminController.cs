using System;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Storage;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/admin")]
    [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class AdminController : ExceptionlessApiController {
        private readonly IFileStorage _fileStorage;
        private readonly IMessagePublisher _messagePublisher;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IQueue<EventPost> _eventPostQueue;

        public AdminController(IFileStorage fileStorage, IMessagePublisher messagePublisher, IOrganizationRepository organizationRepository, IQueue<EventPost> eventPostQueue) {
            _fileStorage = fileStorage;
            _messagePublisher = messagePublisher;
            _organizationRepository = organizationRepository;
            _eventPostQueue = eventPostQueue;
        }

        [HttpPost]
        [Route("change-plan")]
        public async Task<IHttpActionResult> ChangePlanAsync(string organizationId, string planId) {
            if (String.IsNullOrEmpty(organizationId) || !await CanAccessOrganizationAsync(organizationId).AnyContext())
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            var organization = await _organizationRepository.GetByIdAsync(organizationId).AnyContext();
            if (organization == null)
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            var plan = BillingManager.GetBillingPlan(planId);
            if (plan == null)
                return Ok(new { Success = false, Message = "Invalid PlanId." });

            organization.BillingStatus = !String.Equals(plan.Id, BillingManager.FreePlan.Id) ? BillingStatus.Active : BillingStatus.Trialing;
            organization.RemoveSuspension();
            BillingManager.ApplyBillingPlan(organization, plan, await GetExceptionlessUserAsync().AnyContext(), false);

            await _organizationRepository.SaveAsync(organization).AnyContext();
            await _messagePublisher.PublishAsync(new PlanChanged {
                OrganizationId = organization.Id
            }).AnyContext();

            return Ok(new { Success = true });
        }

        [HttpPost]
        [Route("set-bonus")]
        public async Task<IHttpActionResult> SetBonusAsync(string organizationId, int bonusEvents, DateTime? expires = null) {
            if (String.IsNullOrEmpty(organizationId) || !await CanAccessOrganizationAsync(organizationId).AnyContext())
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            var organization = await _organizationRepository.GetByIdAsync(organizationId).AnyContext();
            if (organization == null)
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            organization.BonusEventsPerMonth = bonusEvents;
            organization.BonusExpiration = expires;
            await _organizationRepository.SaveAsync(organization).AnyContext();

            return Ok(new { Success = true });
        }

        [HttpGet]
        [Route("requeue")]
        public async Task<IHttpActionResult> RequeueAsync(string path = null, bool archive = false) {
            if (String.IsNullOrEmpty(path))
                path = @"q\*";

            foreach (var file in await _fileStorage.GetFileListAsync(path).AnyContext())
                await _eventPostQueue.EnqueueAsync(new EventPost { FilePath = file.Path, ShouldArchive = archive }).AnyContext();

            return Ok();
        }
    }
}