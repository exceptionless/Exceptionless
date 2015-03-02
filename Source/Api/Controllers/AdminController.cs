using System;
using System.Web.Http;
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
        public IHttpActionResult ChangePlan(string organizationId, string planId) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            var organization = _organizationRepository.GetById(organizationId);
            if (organization == null)
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            var plan = BillingManager.GetBillingPlan(planId);
            if (plan == null)
                return Ok(new { Success = false, Message = "Invalid PlanId." });

            organization.BillingStatus = !String.Equals(plan.Id, BillingManager.FreePlan.Id) ? BillingStatus.Active : BillingStatus.Trialing;
            organization.RemoveSuspension();
            BillingManager.ApplyBillingPlan(organization, plan, ExceptionlessUser, false);

            _organizationRepository.Save(organization);
            _messagePublisher.Publish(new PlanChanged {
                OrganizationId = organization.Id
            });

            return Ok(new { Success = true });
        }

        [HttpPost]
        [Route("set-bonus")]
        public IHttpActionResult SetBonus(string organizationId, int bonusEvents, DateTime? expires = null) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            var organization = _organizationRepository.GetById(organizationId);
            if (organization == null)
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            organization.BonusEventsPerMonth = bonusEvents;
            organization.BonusExpiration = expires;
            _organizationRepository.Save(organization);

            return Ok(new { Success = true });
        }

        [HttpGet]
        [Route("requeue")]
        public IHttpActionResult Requeue(string path) {
            if (String.IsNullOrEmpty(path))
                path = @"q\*";

            var files = _fileStorage.GetFileList(path);
            foreach (var file in files)
                _eventPostQueue.Enqueue(new EventPost { FilePath = file.Path, ShouldArchive = false });

            return Ok();
        }
    }
}