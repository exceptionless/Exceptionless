using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Api.Hubs;
using Exceptionless.Api.Models.Organization;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Controllers;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Models;
using Exceptionless.Models.Stats;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using NLog.Fluent;
using Stripe;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "organization")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class OrganizationController : ExceptionlessApiController {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly BillingManager _billingManager;
        private readonly NotificationSender _notificationSender;
        private readonly ProjectController _projectController;
        private readonly IMailer _mailer;

        public OrganizationController(IOrganizationRepository organizationRepository, IUserRepository userRepository, IProjectRepository projectRepository, BillingManager billingManager, NotificationSender notificationSender, ProjectController projectController, IMailer mailer) {
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _projectRepository = projectRepository;
            _billingManager = billingManager;
            _notificationSender = notificationSender;
            _projectController = projectController;
            _mailer = mailer;
        }

        [Route]
        [HttpGet]
        public IEnumerable<Organization> Get() {
            return _organizationRepository.GetByIds(Request.GetAssociatedOrganizationIds());
        }

        [HttpGet]
        [Route("{id}/payments")]
        public IHttpActionResult Payments(string id, int page = 1, int pageSize = 12) {
            if (String.IsNullOrWhiteSpace(id) || !User.CanAccessOrganization(id))
                return NotFound();

            Organization organization = _organizationRepository.GetByIdCached(id);
            if (organization == null || String.IsNullOrWhiteSpace(organization.StripeCustomerId))
                return NotFound();

            pageSize = GetPageSize(pageSize);
            int skip = GetSkip(page, pageSize);

            // TODO: implement proper paging once it's supported by the api.
            var limit = pageSize * skip;
            if (limit > 100)
                limit = 100;

            var invoiceService = new StripeInvoiceService();
            List<InvoiceGridModel> invoices = invoiceService.List(new StripeInvoiceListOptions { CustomerId = organization.StripeCustomerId, Limit = limit }).Select(Mapper.Map<InvoiceGridModel>).ToList();
            return Ok(new PagedResult<InvoiceGridModel>(invoices.Skip(skip).Take(pageSize).ToList()) {
                Page = page,
                PageSize = pageSize
            });
        }

        [Route]
        [HttpPost]
        protected IHttpActionResult Post(Organization value) {
            if (value == null || String.IsNullOrEmpty(value.Name))
                return BadRequest();

            if (!IsNameAvailable(value.Name))
                return Conflict();

            var user = Request.GetUser();
            if (!_billingManager.CanAddOrganization(user))
                return this.PlanLimitReached("Please upgrade your plan to add an additional organization.");

            _billingManager.ApplyBillingPlan(value, Settings.Current.EnableBilling ? BillingManager.FreePlan : BillingManager.UnlimitedPlan, user);

            value.Id = null;
            value.ProjectCount = 0;
            value.StackCount = 0;
            value.ErrorCount = 0;
            value.TotalErrorCount = 0;

            Organization organization = _organizationRepository.Add(value);

            // TODO: Ensure that the owin context contains the most up-to-date version.
            //User user = _userRepository.GetById(User.UserEntity.Id);
            user.OrganizationIds.Add(organization.Id);
            _userRepository.Update(user);

            _notificationSender.OrganizationUpdated(organization.Id);

            return Ok(organization);
        }

        [HttpPost]
        [Route("{id}/change-plan")]
        public IHttpActionResult ChangePlan(string id, string planId, string stripeToken, string last4) {
            if (String.IsNullOrEmpty(id) || !User.CanAccessOrganization(id))
                throw new ArgumentException("Invalid organization id.", "id"); // TODO: These should probably throw http Response exceptions.

            if (!Settings.Current.EnableBilling)
                return Ok(new { Success = false, Message = "Plans cannot be changed while billing is disabled." });

            Organization organization = _organizationRepository.GetById(id);
            if (organization == null)
                return Ok(new { Success = false, Message = "Invalid OrganizationId." });

            BillingPlan plan = _billingManager.GetBillingPlan(planId);
            if (plan == null)
                return Ok(new { Success = false, Message = "Invalid PlanId." });

            if (String.Equals(organization.PlanId, plan.Id) && String.Equals(BillingManager.FreePlan.Id, plan.Id))
                return Ok(new { Success = true, Message = "Your plan was not changed as you were already on the free plan." });

            // Only see if they can downgrade a plan if the plans are different.
            string message;
            if (!String.Equals(organization.PlanId, plan.Id) && !_billingManager.CanDownGrade(organization, plan, Request.GetUser(), out message))
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

                _billingManager.ApplyBillingPlan(organization, plan, Request.GetUser());
                _organizationRepository.Update(organization);

                _notificationSender.PlanChanged(organization.Id);
            } catch (Exception e) {
                Log.Error().Exception(e).Message("An error occurred while trying to update your billing plan: " + e.Message).Report(r => r.MarkAsCritical()).Write();
                return Ok(new { Success = false, Message = e.Message });
            }

            return Ok(new { Success = true });
        }

        [HttpPost]
        [Route("{id}/invite")]
        public IHttpActionResult Invite(string id, string emailAddress) {
            if (String.IsNullOrEmpty(id) || !User.CanAccessOrganization(id) || String.IsNullOrEmpty(emailAddress))
                return BadRequest();

            Organization organization = _organizationRepository.GetById(id);
            if (organization == null)
                return BadRequest();

            if (!_billingManager.CanAddUser(organization))
                return this.PlanLimitReached("Please upgrade your plan to add an additional user.");

            var currentUser = Request.GetUser();
            User user = _userRepository.GetByEmailAddress(emailAddress);
            if (user != null) {
                if (!user.OrganizationIds.Contains(organization.Id)) {
                    user.OrganizationIds.Add(organization.Id);
                    _userRepository.Update(user);
                }

                _mailer.SendAddedToOrganizationAsync(currentUser, organization, user);
            } else {
                Invite invite = organization.Invites.FirstOrDefault(i => String.Equals(i.EmailAddress, emailAddress, StringComparison.OrdinalIgnoreCase));
                if (invite == null) {
                    invite = new Invite {
                        Token = Guid.NewGuid().ToString("N").ToLower(),
                        EmailAddress = emailAddress,
                        DateAdded = DateTime.UtcNow
                    };
                    organization.Invites.Add(invite);
                    _organizationRepository.Update(organization);
                }

                _mailer.SendInviteAsync(currentUser, organization, invite);
            }

            _notificationSender.OrganizationUpdated(organization.Id);
            if (user != null)
                return Ok(new User { EmailAddress = user.EmailAddress });

            return Ok();
        }

        [HttpGet]
        [Route("list")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public PagedResult<Organization> List(string criteria = null, bool? isPaidPlan = null, bool? isSuspended = null, OrganizationSortBy sortBy = OrganizationSortBy.Newest, int page = 1, int pageSize = 10) {
            var queries = new List<IMongoQuery>();
            if (!String.IsNullOrWhiteSpace(criteria))
                queries.Add(Query.Matches(OrganizationRepository.FieldNames.Name, new BsonRegularExpression(String.Format("/{0}/i", criteria))));

            if (isPaidPlan.HasValue) {
                if (isPaidPlan.Value)
                    queries.Add(Query.NE(OrganizationRepository.FieldNames.PlanId, new BsonString(BillingManager.FreePlan.Id)));
                else
                    queries.Add(Query.EQ(OrganizationRepository.FieldNames.PlanId, new BsonString(BillingManager.FreePlan.Id)));
            }

            if (isSuspended.HasValue) {
                if (isSuspended.Value)
                    queries.Add(
                        Query.Or(
                            Query.And(
                                Query.NE(OrganizationRepository.FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Active)),
                                Query.NE(OrganizationRepository.FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Trialing)),
                                Query.NE(OrganizationRepository.FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Canceled))),
                            Query.EQ(OrganizationRepository.FieldNames.IsSuspended, new BsonBoolean(true))));
                else
                    queries.Add(Query.And(
                            Query.Or(
                                Query.EQ(OrganizationRepository.FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Active)),
                                Query.EQ(OrganizationRepository.FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Trialing)),
                                Query.EQ(OrganizationRepository.FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Canceled))),
                            Query.EQ(OrganizationRepository.FieldNames.IsSuspended, new BsonBoolean(false))));
            }

            SortByBuilder sort;
            if (sortBy == OrganizationSortBy.Newest)
                sort = SortBy.Descending(OrganizationRepository.FieldNames.Id);
            else if (sortBy == OrganizationSortBy.Subscribed)
                sort = SortBy.Descending(OrganizationRepository.FieldNames.SubscribeDate);
            else if (sortBy == OrganizationSortBy.MostActive)
                sort = SortBy.Descending(OrganizationRepository.FieldNames.TotalErrorCount);
            else
                sort = SortBy.Ascending(OrganizationRepository.FieldNames.Name);

            pageSize = GetPageSize(pageSize);
            int skip = GetSkip(page, pageSize);

            MongoCursor<Organization> query = queries.Count > 0 
                ? ((OrganizationRepository)_organizationRepository).Collection.Find(Query.And(queries)) 
                : ((OrganizationRepository)_organizationRepository).Collection.FindAll();
            
            List<Organization> results = query.SetSortOrder(sort).SetSkip(skip).SetLimit(pageSize).ToList();
            return new PagedResult<Organization>(results, query.Count()) {
                Page = page,
                PageSize = pageSize
            };
        }

        //protected override bool CanUpdateEntity(Organization original, Delta<Organization> value)
        //{
        //    Organization organization = value.GetEntity();
        //    if (value.ContainsChangedProperty(t => t.Id)
        //        && !String.Equals(original.Id, organization.Id, StringComparison.OrdinalIgnoreCase))
        //        return false;

        //    if (value.ContainsChangedProperty(t => t.Name)
        //        && !String.Equals(original.Name, organization.Name, StringComparison.OrdinalIgnoreCase)
        //        && !IsNameAvailable(organization.Name))
        //        return false;

        //    if ((value.ContainsChangedProperty(t => t.MaxErrorsPerDay) && original.MaxErrorsPerDay != organization.MaxErrorsPerDay)
        //        || (value.ContainsChangedProperty(t => t.LastErrorDate) && original.LastErrorDate != organization.LastErrorDate)
        //        || (value.ContainsChangedProperty(t => t.StripeCustomerId) && original.StripeCustomerId != organization.StripeCustomerId)
        //        || (value.ContainsChangedProperty(t => t.PlanId) && original.PlanId != organization.PlanId)
        //        || (value.ContainsChangedProperty(t => t.CardLast4) && original.CardLast4 != organization.CardLast4)
        //        || (value.ContainsChangedProperty(t => t.SubscribeDate) && original.SubscribeDate != organization.SubscribeDate)
        //        || (value.ContainsChangedProperty(t => t.BillingChangeDate) && original.BillingChangeDate != organization.BillingChangeDate)
        //        || (value.ContainsChangedProperty(t => t.RetentionDays) && original.RetentionDays != organization.RetentionDays)
        //        || (value.ContainsChangedProperty(t => t.MaxErrorsPerDay) && original.MaxErrorsPerDay != organization.MaxErrorsPerDay)
        //        || (value.ContainsChangedProperty(t => t.MaxProjects) && original.MaxProjects != organization.MaxProjects)
        //        || (value.ContainsChangedProperty(t => t.ProjectCount) && original.ProjectCount != organization.ProjectCount)
        //        || (value.ContainsChangedProperty(t => t.StackCount) && original.StackCount != organization.StackCount)
        //        || (value.ContainsChangedProperty(t => t.ErrorCount) && original.ErrorCount != organization.ErrorCount)
        //        || (value.ContainsChangedProperty(t => t.TotalErrorCount) && original.TotalErrorCount != organization.TotalErrorCount)
        //        || (value.ContainsChangedProperty(t => t.SuspensionDate) && original.SuspensionDate != organization.SuspensionDate)
        //        || (value.ContainsChangedProperty(t => t.SuspendedByUserId) && original.SuspendedByUserId != organization.SuspendedByUserId))
        //        return false;

        //    if (!User.IsInRole(AuthorizationRoles.GlobalAdmin)
        //        && ((value.ContainsChangedProperty(t => t.IsSuspended) && original.IsSuspended != organization.IsSuspended)
        //            || (value.ContainsChangedProperty(t => t.SuspensionCode) && original.SuspensionCode != organization.SuspensionCode)
        //            || (value.ContainsChangedProperty(t => t.SuspensionNotes) && original.SuspensionNotes != organization.SuspensionNotes)))
        //        return false;

        //    return base.CanUpdateEntity(original, value);
        //}

        //protected override Organization UpdateEntity(Organization original, Delta<Organization> value)
        //{
        //    if (String.IsNullOrWhiteSpace(original.Id) || !User.CanAccessOrganization(original.Id))
        //        return BadRequest();

        //    Organization organization = value.GetEntity();
        //    bool suspendedStateChanged = (value.ContainsChangedProperty(t => t.IsSuspended) && original.IsSuspended != organization.IsSuspended)
        //                                 || (value.ContainsChangedProperty(t => t.SuspensionCode) && original.SuspensionCode != organization.SuspensionCode)
        //                                 || (value.ContainsChangedProperty(t => t.SuspensionNotes) && original.SuspensionNotes != organization.SuspensionNotes);

        //    value.Patch(original);

        //    if (suspendedStateChanged)
        //    {
        //        if (original.IsSuspended)
        //        {
        //            original.SuspensionDate = DateTime.Now;
        //            original.SuspendedByUserId = User.UserEntity != null ? User.UserEntity.Id : null;
        //            if (!String.Equals(original.SuspensionCode, SuspensionCodes.Abuse) && !String.Equals(original.SuspensionCode, SuspensionCodes.Billing) && !String.Equals(original.SuspensionCode, SuspensionCodes.Overage))
        //                original.SuspensionCode = SuspensionCodes.Other;
        //        }
        //        else
        //        {
        //            original.SuspensionDate = null;
        //            original.SuspensionCode = null;
        //            original.SuspensionNotes = null;
        //            original.SuspendedByUserId = null;
        //        }
        //    }

        //    organization = _organizationRepository.Update(original);
        //    _notificationSender.OrganizationUpdated(organization.Id);

        //    return organization;
        //}

        [HttpDelete]
        [Route("{id}")]
        public IHttpActionResult Delete(string id) {
            if (String.IsNullOrWhiteSpace(id) || !User.CanAccessOrganization(id))
                return BadRequest();

            Organization value = _organizationRepository.GetById(id);
            if (value == null)
                return BadRequest();

            if (!String.IsNullOrEmpty(value.StripeCustomerId) && User.IsInRole(AuthorizationRoles.GlobalAdmin))
                return BadRequest("An organization cannot be deleted if it has a subscription.");

            List<Project> projects = _projectRepository.GetByOrganizationId(value.Id).ToList();
            if (!User.IsInRole(AuthorizationRoles.GlobalAdmin) && projects.Any())
                return BadRequest("An organization cannot be deleted if it contains any projects.");

            var currentUser = Request.GetUser();
            Log.Info().Message("User {0} deleting organization {1} with {2} errors.", currentUser.Id, value.Id, value.ErrorCount).Write();

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
                    _userRepository.Delete(user.Id);
                } else {
                    Log.Info().Message("Removing user '{0}' from organization '{1}' with Id: '{2}'", user.Id, value.Name, value.Id).Write();
                    user.OrganizationIds.Remove(value.Id);
                    _userRepository.Update(user);
                }
            }

            if (User.IsInRole(AuthorizationRoles.GlobalAdmin) && projects.Count > 0) {
                foreach (Project project in projects) {
                    Log.Info().Message("Resetting all project data for project '{0}' with Id: '{1}'.", project.Name, project.Id).Write();
                    _projectController.ResetData(project.Id);
                }

                Log.Info().Message("Deleting all projects for organization '{0}' with Id: '{1}'.", value.Name, value.Id).Write();
                _projectRepository.Delete(projects);
            }

            Log.Info().Message("Deleting organization '{0}' with Id: '{1}'.", value.Name, value.Id).Write();
            _organizationRepository.Delete(value);

            _notificationSender.OrganizationUpdated(value.Id);

            return Ok();
        }

        [HttpDelete]
        [Route("{id}/remove-user/{emailAddress}")]
        public IHttpActionResult RemoveUser(string id, string emailAddress) {
            if (String.IsNullOrEmpty(id) || !User.CanAccessOrganization(id) || String.IsNullOrEmpty(emailAddress))
                return BadRequest();

            Organization organization = _organizationRepository.GetById(id);
            if (organization == null)
                return BadRequest();

            User user = _userRepository.GetByEmailAddress(emailAddress); // TODO This should be by email address.
            if (user == null || !user.OrganizationIds.Contains(id)) {
                Invite invite = organization.Invites.FirstOrDefault(i => String.Equals(i.EmailAddress, emailAddress, StringComparison.OrdinalIgnoreCase));
                if (invite != null) {
                    organization.Invites.Remove(invite);
                    _organizationRepository.Update(organization);
                }
            } else {
                if (!user.OrganizationIds.Contains(organization.Id))
                    return BadRequest();

                if (_userRepository.GetByOrganizationId(organization.Id).Count() == 1)
                    return BadRequest("An organization must contain at least one user.");

                List<Project> projects = _projectRepository.GetByOrganizationId(organization.Id).Where(p => p.NotificationSettings.ContainsKey(user.Id)).ToList();
                if (projects.Count > 0) {
                    foreach (Project project in projects)
                        project.NotificationSettings.Remove(user.Id);

                    _projectRepository.Update(projects);
                }

                user.OrganizationIds.Remove(organization.Id);
                _userRepository.Update(user);
            }

            _notificationSender.OrganizationUpdated(organization.Id);
            return Ok();
        }

        private bool IsNameAvailable(string name) {
            if (String.IsNullOrEmpty(name))
                return false;

            return _organizationRepository.Count(
                Query.And(
                     Query.In(OrganizationRepository.FieldNames.Id, Request.GetAssociatedOrganizationIds().Select(id => new BsonObjectId(new ObjectId(id)))),
                     Query.EQ(OrganizationRepository.FieldNames.Name, name))) == 0;
        }

        public enum OrganizationSortBy {
            Newest = 0,
            Subscribed = 1,
            MostActive = 2,
            Alphabetical = 3,
        }
    }
}