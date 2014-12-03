using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Api.Models;
using Exceptionless.Api.Models.Organization;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories;
using Exceptionless.Api.Utility;
using Exceptionless.Models;
using NLog.Fluent;
using Stripe;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/organizations")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class OrganizationController : RepositoryApiController<IOrganizationRepository, Organization, ViewOrganization, NewOrganization, NewOrganization> {
        private readonly IUserRepository _userRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly BillingManager _billingManager;
        private readonly ProjectController _projectController;
        private readonly IMailer _mailer;

        public OrganizationController(IOrganizationRepository organizationRepository, IUserRepository userRepository, IProjectRepository projectRepository, BillingManager billingManager, ProjectController projectController, IMailer mailer) : base(organizationRepository) {
            _userRepository = userRepository;
            _projectRepository = projectRepository;
            _billingManager = billingManager;
            _projectController = projectController;
            _mailer = mailer;
        }

        #region CRUD

        [HttpGet]
        [Route]
        public IHttpActionResult Get(int page = 1, int limit = 10) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var options = new PagingOptions { Page = page, Limit = limit };
            var results = _repository.GetByIds(GetAssociatedOrganizationIds(), options).Select(Mapper.Map<Organization, ViewOrganization>).ToList();
            return OkWithResourceLinks(results, options.HasMore && !NextPageExceedsSkipLimit(page, limit), page);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/admin/organizations")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public IHttpActionResult GetForAdmins(string criteria = null, bool? paid = null, bool? suspended = null, int page = 1, int limit = 10, OrganizationSortBy sort = OrganizationSortBy.Newest) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var options = new PagingOptions { Page = page, Limit = limit };
            var results = _repository.GetByCriteria(criteria, options, sort, paid, suspended).Select(Mapper.Map<Organization, ViewOrganization>).ToList();
            return OkWithResourceLinks(results, options.HasMore, page);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/admin/organizations/stats")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public IHttpActionResult PlanStats() {
            return Ok(_repository.GetBillingPlanStats());
        }

        [HttpGet]
        [Route("{id:objectid}", Name = "GetOrganizationById")]
        public override IHttpActionResult GetById(string id) {
            return base.GetById(id);
        }

        [HttpPost]
        [Route]
        public override IHttpActionResult Post(NewOrganization value) {
            return base.Post(value);
        }

        [HttpPatch]
        [HttpPut]
        [Route("{id:objectid}")]
        public override IHttpActionResult Patch(string id, Delta<NewOrganization> changes) {
            return base.Patch(id, changes);
        }

        [HttpDelete]
        [Route("{ids:objectids}")]
        public override IHttpActionResult Delete([CommaDelimitedArray]string[] ids) {
            return base.Delete(ids);
        }

        #endregion

        [HttpGet]
        [Route("{id:objectid}/invoices")]
        public IHttpActionResult GetInvoices(string id, string before = null, string after = null, int limit = 12) {
            if (String.IsNullOrWhiteSpace(id) || !CanAccessOrganization(id))
                return NotFound();

            Organization organization = _repository.GetById(id, true);
            if (organization == null)
                return NotFound();

            if (String.IsNullOrWhiteSpace(organization.StripeCustomerId))
                return Ok(new List<InvoiceGridModel>());

            var invoiceService = new StripeInvoiceService();
            var invoices = invoiceService.List(new StripeInvoiceListOptions { CustomerId = organization.StripeCustomerId, Limit = limit + 1, EndingBefore = before, StartingAfter = after }).Select(Mapper.Map<InvoiceGridModel>).ToList();

            return OkWithResourceLinks(invoices.Take(limit).ToList(), invoices.Count > limit, i => i.Id);
        }

        [HttpPost]
        [Route("{id:objectid}/change-plan")]
        public IHttpActionResult ChangePlan(string id, string planId, string stripeToken = null, string last4 = null) {
            if (String.IsNullOrEmpty(id) || !CanAccessOrganization(id))
                return BadRequest("Invalid organization id.");

            if (!Settings.Current.EnableBilling)
                return Ok(new { Success = false, Message = "Plans cannot be changed while billing is disabled." });

            Organization organization = _repository.GetById(id);
            if (organization == null)
                return Ok(new { Success = false, Message = "Invalid OrganizationId." });

            BillingPlan plan = _billingManager.GetBillingPlan(planId);
            if (plan == null)
                return Ok(new { Success = false, Message = "Invalid PlanId." });

            if (String.Equals(organization.PlanId, plan.Id) && String.Equals(BillingManager.FreePlan.Id, plan.Id))
                return Ok(new { Success = true, Message = "Your plan was not changed as you were already on the free plan." });

            // Only see if they can downgrade a plan if the plans are different.
            string message;
            if (!String.Equals(organization.PlanId, plan.Id) && !_billingManager.CanDownGrade(organization, plan, ExceptionlessUser, out message))
                return Ok(new { Success = false, Message = message });

            var customerService = new StripeCustomerService();
            var subscriptionService = new StripeSubscriptionService();

            try {
                // If they are on a paid plan and then downgrade to a free plan then cancel their stripe subscription.
                if (!String.Equals(organization.PlanId, BillingManager.FreePlan.Id) && String.Equals(plan.Id, BillingManager.FreePlan.Id)) {
                    if (!String.IsNullOrEmpty(organization.StripeCustomerId)) {
                        var subs = subscriptionService.List(organization.StripeCustomerId).Where(s => !s.CanceledAt.HasValue);
                        foreach (var sub in subs)
                            subscriptionService.Cancel(organization.StripeCustomerId, sub.Id);
                    }

                    organization.BillingStatus = BillingStatus.Trialing;
                    organization.RemoveSuspension();
                } else if (String.IsNullOrEmpty(organization.StripeCustomerId)) {
                    if (String.IsNullOrEmpty(stripeToken))
                        return Ok(new { Success = false, Message = "Billing information was not set." });

                    organization.SubscribeDate = DateTime.Now;

                    StripeCustomer customer = customerService.Create(new StripeCustomerCreateOptions {
                        TokenId = stripeToken,
                        PlanId = planId,
                        Description = organization.Name
                    });

                    organization.BillingStatus = BillingStatus.Active;
                    organization.RemoveSuspension();
                    organization.StripeCustomerId = customer.Id;
                    if (customer.StripeCardList.StripeCards.Count > 0)
                        organization.CardLast4 = customer.StripeCardList.StripeCards[0].Last4;
                } else {
                    var update = new StripeSubscriptionUpdateOptions {  PlanId = planId };
                    var create = new StripeSubscriptionCreateOptions();
                    bool cardUpdated = false;

                    if (!String.IsNullOrEmpty(stripeToken)) {
                        update.TokenId = stripeToken;
                        create.TokenId = stripeToken;
                        cardUpdated = true;
                    }
                    
                    var subscription = subscriptionService.List(organization.StripeCustomerId).FirstOrDefault(s => !s.CanceledAt.HasValue);
                    if (subscription != null)
                        subscriptionService.Update(organization.StripeCustomerId, subscription.Id, update);
                    else
                        subscriptionService.Create(organization.StripeCustomerId, planId, create);

                    if (cardUpdated)
                        organization.CardLast4 = last4;

                    organization.BillingStatus = BillingStatus.Active;
                    organization.RemoveSuspension();
                }

                _billingManager.ApplyBillingPlan(organization, plan, ExceptionlessUser);
                _repository.Save(organization);
            } catch (Exception e) {
                Log.Error().Exception(e).Message("An error occurred while trying to update your billing plan: " + e.Message).Report(r => r.MarkAsCritical()).Write();
                return Ok(new { Success = false, Message = e.Message });
            }

            return Ok(new { Success = true });
        }

        [HttpPost]
        [Route("{id:objectid}/users/{email:minlength(1)}")]
        public async Task<IHttpActionResult> AddUser(string id, string email) {
            if (String.IsNullOrEmpty(id) || !CanAccessOrganization(id) || String.IsNullOrEmpty(email))
                return BadRequest();

            Organization organization = _repository.GetById(id);
            if (organization == null)
                return BadRequest();

            if (!_billingManager.CanAddUser(organization))
                return PlanLimitReached("Please upgrade your plan to add an additional user.");

            var currentUser = ExceptionlessUser;
            User user = _userRepository.GetByEmailAddress(email);
            if (user != null) {
                if (!user.OrganizationIds.Contains(organization.Id)) {
                    user.OrganizationIds.Add(organization.Id);
                    _userRepository.Save(user);
                }

                _mailer.SendAddedToOrganization(currentUser, organization, user);
            } else {
                Invite invite = organization.Invites.FirstOrDefault(i => String.Equals(i.EmailAddress, email, StringComparison.OrdinalIgnoreCase));
                if (invite == null) {
                    invite = new Invite {
                        Token = Guid.NewGuid().ToString("N").ToLower(),
                        EmailAddress = email,
                        DateAdded = DateTime.UtcNow
                    };
                    organization.Invites.Add(invite);
                    _repository.Save(organization);
                }

                _mailer.SendInvite(currentUser, organization, invite);
            }

            if (user != null)
                return Ok(new User { EmailAddress = user.EmailAddress });

            return Ok();
        }

        [HttpDelete]
        [Route("{id:objectid}/users/{email:minlength(1)}")]
        public IHttpActionResult RemoveUser(string id, string email) {
            if (String.IsNullOrEmpty(id) || !CanAccessOrganization(id) || String.IsNullOrEmpty(email))
                return BadRequest();

            Organization organization = _repository.GetById(id);
            if (organization == null)
                return BadRequest();

            User user = _userRepository.GetByEmailAddress(email);
            if (user == null || !user.OrganizationIds.Contains(id)) {
                Invite invite = organization.Invites.FirstOrDefault(i => String.Equals(i.EmailAddress, email, StringComparison.OrdinalIgnoreCase));
                if (invite == null)
                    return Ok();

                organization.Invites.Remove(invite);
                _repository.Save(organization);
            } else {
                if (!user.OrganizationIds.Contains(organization.Id))
                    return BadRequest();

                if (_userRepository.GetByOrganizationId(organization.Id).Count() == 1)
                    return BadRequest("An organization must contain at least one user.");

                List<Project> projects = _projectRepository.GetByOrganizationId(organization.Id).Where(p => p.NotificationSettings.ContainsKey(user.Id)).ToList();
                if (projects.Count > 0) {
                    foreach (Project project in projects)
                        project.NotificationSettings.Remove(user.Id);

                    _projectRepository.Save(projects);
                }

                user.OrganizationIds.Remove(organization.Id);
                _userRepository.Save(user);
            }

            return Ok();
        }

        [HttpPost]
        [Route("{id:objectid}/suspend")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public IHttpActionResult Suspend(string id, SuspensionCode code, string notes = null) {
            var organization = GetModel(id, false);
            if (organization == null)
                return BadRequest();

            organization.IsSuspended = true;
            organization.SuspensionDate = DateTime.Now;
            organization.SuspendedByUserId = ExceptionlessUser.Id;
            organization.SuspensionCode = code;
            organization.SuspensionNotes = notes;
            _repository.Save(organization);

            return Ok();
        }

        [HttpDelete]
        [Route("{id:objectid}/suspend")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public IHttpActionResult Unsuspend(string id) {
            var organization = GetModel(id, false);
            if (organization == null)
                return BadRequest();

            organization.IsSuspended = false;
            organization.SuspensionDate = null;
            organization.SuspendedByUserId = null;
            organization.SuspensionCode = null;
            organization.SuspensionNotes = null;
            _repository.Save(organization);

            return Ok();
        }

        [HttpPost]
        [Route("{id:objectid}/data/{key:minlength(1)}")]
        public IHttpActionResult PostData(string id, string key, string value) {
            var organization = GetModel(id, false);
            if (organization == null)
                return BadRequest();

            organization.Data[key] = value;
            _repository.Save(organization);

            return Ok();
        }

        [HttpDelete]
        [Route("{id:objectid}/data/{key:minlength(1)}")]
        public IHttpActionResult DeleteData(string id, string key) {
            var organization = GetModel(id, false);
            if (organization == null)
                return BadRequest();

            if (organization.Data.Remove(key))
                _repository.Save(organization);

            return Ok();
        }

        [HttpGet]
        [Route("check-name/{name:minlength(1)}")]
        public IHttpActionResult IsNameAvailable(string name) {
            if (IsNameAvailableInternal(name))
                return NotFound();

            return Ok();
        }

        private bool IsNameAvailableInternal(string name) {
            return !String.IsNullOrWhiteSpace(name) && _repository.GetByIds(GetAssociatedOrganizationIds()).Any(o => o.Name.Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        protected override PermissionResult CanAdd(Organization value) {
            if (String.IsNullOrEmpty(value.Name))
                return PermissionResult.DenyWithMessage("Organization name is required.");

            if (!IsNameAvailableInternal(value.Name))
                return PermissionResult.DenyWithMessage("A organization with this name already exists.");

            if (!_billingManager.CanAddOrganization(ExceptionlessUser))
                return PermissionResult.DenyWithPlanLimitReached("Please upgrade your plan to add an additional organization.");

            return base.CanAdd(value);
        }

        protected override Organization AddModel(Organization value) {
            _billingManager.ApplyBillingPlan(value, Settings.Current.EnableBilling ? BillingManager.FreePlan : BillingManager.UnlimitedPlan, ExceptionlessUser);

            var organization = base.AddModel(value);

            ExceptionlessUser.OrganizationIds.Add(organization.Id);
            _userRepository.Save(ExceptionlessUser);

            return organization;
        }

        protected override PermissionResult CanUpdate(Organization original, Delta<NewOrganization> changes) {
            var changed = changes.GetEntity();
            if (!IsNameAvailableInternal(changed.Name))
                return PermissionResult.DenyWithPlanLimitReached("A organization with this name already exists.");

            return base.CanUpdate(original, changes);
        }

        protected override PermissionResult CanDelete(Organization value) {
            if (!String.IsNullOrEmpty(value.StripeCustomerId) && User.IsInRole(AuthorizationRoles.GlobalAdmin))
                return PermissionResult.DenyWithMessage("An organization cannot be deleted if it has a subscription.", value.Id);

            List<Project> projects = _projectRepository.GetByOrganizationId(value.Id).ToList();
            if (!User.IsInRole(AuthorizationRoles.GlobalAdmin) && projects.Any())
                return PermissionResult.DenyWithMessage("An organization cannot be deleted if it contains any projects.", value.Id);

            return base.CanDelete(value);
        }

        protected override void DeleteModels(ICollection<Organization> organizations) {
            var currentUser = ExceptionlessUser;

            foreach (var organization in organizations) {
                Log.Info().Message("User {0} deleting organization {1} with {2} total events.", currentUser.Id, organization.Id, organization.TotalEventCount).Write();

                if (!String.IsNullOrEmpty(organization.StripeCustomerId)) {
                    Log.Info().Message("Canceling stripe subscription for the organization '{0}' with Id: '{1}'.", organization.Name, organization.Id).Write();

                    var subscriptionService = new StripeSubscriptionService();
                    var subs = subscriptionService.List(organization.StripeCustomerId).Where(s => !s.CanceledAt.HasValue);
                    foreach (var sub in subs)
                        subscriptionService.Cancel(organization.StripeCustomerId, sub.Id);
                }

                List<User> users = _userRepository.GetByOrganizationId(organization.Id).ToList();
                foreach (User user in users) {
                    // delete the user if they are not associated to any other organizations and they are not the current user
                    if (user.OrganizationIds.All(oid => String.Equals(oid, organization.Id)) && !String.Equals(user.Id, currentUser.Id)) {
                        Log.Info().Message("Removing user '{0}' as they do not belong to any other organizations.", user.Id, organization.Name, organization.Id).Write();
                        _userRepository.Remove(user.Id);
                    } else {
                        Log.Info().Message("Removing user '{0}' from organization '{1}' with Id: '{2}'", user.Id, organization.Name, organization.Id).Write();
                        user.OrganizationIds.Remove(organization.Id);
                        _userRepository.Save(user);
                    }
                }

                List<Project> projects = _projectRepository.GetByOrganizationId(organization.Id).ToList();
                if (User.IsInRole(AuthorizationRoles.GlobalAdmin) && projects.Count > 0) {
                    foreach (Project project in projects) {
                        Log.Info().Message("Resetting all project data for project '{0}' with Id: '{1}'.", project.Name, project.Id).Write();
                        _projectController.ResetDataAsync(project.Id).Wait();
                    }

                    Log.Info().Message("Deleting all projects for organization '{0}' with Id: '{1}'.", organization.Name, organization.Id).Write();
                    _projectRepository.Save(projects);
                }

                Log.Info().Message("Deleting organization '{0}' with Id: '{1}'.", organization.Name, organization.Id).Write();
                base.DeleteModels(new[] { organization });
            }
        }
    }
}