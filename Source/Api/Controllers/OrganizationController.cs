using System;
using System.Collections.Generic;
using System.Linq;
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
using Exceptionless.Core.Web;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Driver.Builders;
using NLog.Fluent;
using Stripe;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/organization")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class OrganizationController : RepositoryApiController<OrganizationRepository, Organization, ViewOrganization, NewOrganization, NewOrganization> {
        private readonly IUserRepository _userRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly BillingManager _billingManager;
        private readonly ProjectController _projectController;
        private readonly IMailer _mailer;

        public OrganizationController(OrganizationRepository organizationRepository, IUserRepository userRepository, IProjectRepository projectRepository, BillingManager billingManager, ProjectController projectController, IMailer mailer) : base(organizationRepository) {
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
            var options = new GetEntitiesOptions { Page = page, Limit = limit };
            var results = GetEntities<ViewOrganization>(options);
            return OkWithResourceLinks(results, options.HasMore);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/admin/organization")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public IHttpActionResult GetForAdmins(string criteria = null, bool? paid = null, bool? suspended = null, OrganizationSortBy sort = OrganizationSortBy.Newest, int page = 1, int limit = 10) {
            var options = new GetEntitiesOptions { Page = page, Limit = limit };
            
            if (!String.IsNullOrWhiteSpace(criteria))
                options.Query = options.Query.And(Query.Matches(OrganizationRepository.FieldNames.Name, new BsonRegularExpression(String.Format("/{0}/i", criteria))));

            if (paid.HasValue) {
                if (paid.Value)
                    options.Query = options.Query.And(Query.NE(OrganizationRepository.FieldNames.PlanId, new BsonString(BillingManager.FreePlan.Id)));
                else
                    options.Query = options.Query.And(Query.EQ(OrganizationRepository.FieldNames.PlanId, new BsonString(BillingManager.FreePlan.Id)));
            }

            if (suspended.HasValue) {
                if (suspended.Value)
                    options.Query = options.Query.And(
                        Query.Or(
                            Query.And(
                                Query.NE(OrganizationRepository.FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Active)),
                                Query.NE(OrganizationRepository.FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Trialing)),
                                Query.NE(OrganizationRepository.FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Canceled))),
                            Query.EQ(OrganizationRepository.FieldNames.IsSuspended, new BsonBoolean(true))));
                else
                    options.Query = options.Query.And(
                            Query.Or(
                                Query.EQ(OrganizationRepository.FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Active)),
                                Query.EQ(OrganizationRepository.FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Trialing)),
                                Query.EQ(OrganizationRepository.FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Canceled))),
                            Query.EQ(OrganizationRepository.FieldNames.IsSuspended, new BsonBoolean(false)));
            }

            switch (sort) {
                case OrganizationSortBy.Newest:
                    options.SortBy = SortBy.Descending(OrganizationRepository.FieldNames.Id);
                    break;
                case OrganizationSortBy.Subscribed:
                    options.SortBy = SortBy.Descending(OrganizationRepository.FieldNames.SubscribeDate);
                    break;
                case OrganizationSortBy.MostActive:
                    options.SortBy = SortBy.Descending(OrganizationRepository.FieldNames.TotalEventCount);
                    break;
                default:
                    options.SortBy = SortBy.Ascending(OrganizationRepository.FieldNames.Name);
                    break;
            }

            var results = GetEntities<Organization>(options);
            return OkWithResourceLinks(results, options.HasMore, page);
        }

        [HttpGet]
        [Route("{id}", Name = "GetOrganizationById")]
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
        [Route("{id}")]
        public override IHttpActionResult Patch(string id, Delta<NewOrganization> changes) {
            return base.Patch(id, changes);
        }

        [HttpDelete]
        [Route("{id}")]
        public override IHttpActionResult Delete(string id) {
            return base.Delete(id);
        }

        #endregion

        [HttpGet]
        [Route("{id}/invoice")]
        public IHttpActionResult GetInvoices(string id, int limit = 12, string before = null, string after = null) {
            if (String.IsNullOrWhiteSpace(id) || !CanAccessOrganization(id))
                return NotFound();

            Organization organization = _repository.GetById(id, true);
            if (organization == null || String.IsNullOrWhiteSpace(organization.StripeCustomerId))
                return NotFound();

            var invoiceService = new StripeInvoiceService();
            var invoices = invoiceService.List(new StripeInvoiceListOptions { CustomerId = organization.StripeCustomerId, Limit = limit + 1, EndingBefore = before, StartingAfter = after }).Select(Mapper.Map<InvoiceGridModel>).ToList();

            return OkWithResourceLinks(invoices.Take(limit).ToList(), invoices.Count > limit, i => i.Id);
        }

        [HttpPost]
        [Route("{id}/change-plan")]
        public IHttpActionResult ChangePlan(string id, string planId, string stripeToken, string last4) {
            if (String.IsNullOrEmpty(id) || !CanAccessOrganization(id))
                throw new ArgumentException("Invalid organization id.", "id"); // TODO: These should probably throw http Response exceptions.

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

            try {
                // If they are on a paid plan and then downgrade to a free plan then cancel their stripe subscription.
                if (!String.Equals(organization.PlanId, BillingManager.FreePlan.Id) && String.Equals(plan.Id, BillingManager.FreePlan.Id)) {
                    if (!String.IsNullOrEmpty(organization.StripeCustomerId))
                        customerService.CancelSubscription(organization.StripeCustomerId);

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
                    var update = new StripeCustomerUpdateSubscriptionOptions { PlanId = planId };
                    bool cardUpdated = false;

                    if (!String.IsNullOrEmpty(stripeToken)) {
                        update.TokenId = stripeToken;
                        cardUpdated = true;
                    }

                    customerService.UpdateSubscription(organization.StripeCustomerId, update);
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
        [Route("{id}/user/{email}")]
        public IHttpActionResult AddUser(string id, string email) {
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

                _mailer.SendAddedToOrganizationAsync(currentUser, organization, user);
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

                _mailer.SendInviteAsync(currentUser, organization, invite);
            }

            if (user != null)
                return Ok(new User { EmailAddress = user.EmailAddress });

            return Ok();
        }

        [HttpDelete]
        [Route("{id}/user/{email}")]
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
        [Route("{id}/suspend")]
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
        [Route("{id}/suspend")]
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
        [Route("{id}/data/{key}")]
        public IHttpActionResult PostData(string id, string key, string value) {
            var organization = GetModel(id, false);
            if (organization == null)
                return BadRequest();

            organization.Data[key] = value;
            _repository.Save(organization);

            return Ok();
        }

        [HttpDelete]
        [Route("{id}/data/{key}")]
        public IHttpActionResult DeleteData(string id, string key) {
            var organization = GetModel(id, false);
            if (organization == null)
                return BadRequest();

            organization.Data.Remove(key);
            _repository.Save(organization);

            return Ok();
        }

        [HttpGet]
        [Route("check-name")]
        public IHttpActionResult IsNameAvailable(string name) {
            if (String.IsNullOrWhiteSpace(name))
                return NotFound();

            if (_repository.GetByIds(GetAssociatedOrganizationIds()).Any(o => o.Name.Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
                return Ok();

            return NotFound();
        }

        protected override PermissionResult CanAdd(Organization value) {
            if (String.IsNullOrEmpty(value.Name))
                return PermissionResult.DenyWithResult(BadRequest("Organization name is required."));

            if (!_billingManager.CanAddOrganization(ExceptionlessUser))
                return PermissionResult.DenyWithResult(PlanLimitReached("Please upgrade your plan to add an additional organization."));

            return base.CanAdd(value);
        }

        protected override Organization AddModel(Organization value) {
            _billingManager.ApplyBillingPlan(value, Settings.Current.EnableBilling ? BillingManager.FreePlan : BillingManager.UnlimitedPlan, ExceptionlessUser);

            var organization = base.AddModel(value);

            ExceptionlessUser.OrganizationIds.Add(organization.Id);
            _userRepository.Save(ExceptionlessUser);

            return organization;
        }

        protected override PermissionResult CanDelete(Organization value) {
            if (!String.IsNullOrEmpty(value.StripeCustomerId) && User.IsInRole(AuthorizationRoles.GlobalAdmin))
                return PermissionResult.DenyWithResult(BadRequest("An organization cannot be deleted if it has a subscription."));

            List<Project> projects = _projectRepository.GetByOrganizationId(value.Id).ToList();
            if (!User.IsInRole(AuthorizationRoles.GlobalAdmin) && projects.Any())
                return PermissionResult.DenyWithResult(BadRequest("An organization cannot be deleted if it contains any projects."));

            return base.CanDelete(value);
        }

        protected override void DeleteModel(Organization value) {
            var currentUser = ExceptionlessUser;
            Log.Info().Message("User {0} deleting organization {1} with {2} errors.", currentUser.Id, value.Id, value.EventCount).Write();

            if (!String.IsNullOrEmpty(value.StripeCustomerId)) {
                Log.Info().Message("Canceling stripe subscription for the organization '{0}' with Id: '{1}'.", value.Name, value.Id).Write();

                var customerService = new StripeCustomerService();
                customerService.CancelSubscription(value.StripeCustomerId);
            }

            List<User> users = _userRepository.GetByOrganizationId(value.Id).ToList();
            foreach (User user in users) {
                // delete the user if they are not associated to any other organizations and they are not the current user
                if (user.OrganizationIds.All(oid => String.Equals(oid, value.Id)) && !String.Equals(user.Id, currentUser.Id)) {
                    Log.Info().Message("Removing user '{0}' as they do not belong to any other organizations.", user.Id, value.Name, value.Id).Write();
                    _userRepository.Remove(user.Id);
                } else {
                    Log.Info().Message("Removing user '{0}' from organization '{1}' with Id: '{2}'", user.Id, value.Name, value.Id).Write();
                    user.OrganizationIds.Remove(value.Id);
                    _userRepository.Save(user);
                }
            }

            List<Project> projects = _projectRepository.GetByOrganizationId(value.Id).ToList();
            if (User.IsInRole(AuthorizationRoles.GlobalAdmin) && projects.Count > 0) {
                foreach (Project project in projects) {
                    Log.Info().Message("Resetting all project data for project '{0}' with Id: '{1}'.", project.Name, project.Id).Write();
                    _projectController.ResetData(project.Id);
                }

                Log.Info().Message("Deleting all projects for organization '{0}' with Id: '{1}'.", value.Name, value.Id).Write();
                _projectRepository.Save(projects);
            }

            Log.Info().Message("Deleting organization '{0}' with Id: '{1}'.", value.Name, value.Id).Write();
            base.DeleteModel(value);
        }

        public enum OrganizationSortBy {
            Newest = 0,
            Subscribed = 1,
            MostActive = 2,
            Alphabetical = 3,
        }
    }
}