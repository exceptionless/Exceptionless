using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Api.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Models;
using Foundatio.Caching;
using Foundatio.Messaging;
using NLog.Fluent;
using Stripe;
#pragma warning disable 1998

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/organizations")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class OrganizationController : RepositoryApiController<IOrganizationRepository, Organization, ViewOrganization, NewOrganization, NewOrganization> {
        private readonly ICacheClient _cacheClient;
        private readonly IUserRepository _userRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly BillingManager _billingManager;
        private readonly ProjectController _projectController;
        private readonly IMailer _mailer;
        private readonly IMessagePublisher _messagePublisher;
        private readonly EventStats _stats;

        public OrganizationController(IOrganizationRepository organizationRepository, ICacheClient cacheClient, IUserRepository userRepository, IProjectRepository projectRepository, BillingManager billingManager, ProjectController projectController, IMailer mailer, IMessagePublisher messagePublisher, EventStats stats) : base(organizationRepository) {
            _cacheClient = cacheClient;
            _userRepository = userRepository;
            _projectRepository = projectRepository;
            _billingManager = billingManager;
            _projectController = projectController;
            _mailer = mailer;
            _messagePublisher = messagePublisher;
            _stats = stats;
        }

        #region CRUD

        [HttpGet]
        [Route]
        public IHttpActionResult Get(int page = 1, int limit = 10) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var options = new PagingOptions { Page = page, Limit = limit };
            var organizations = Enumerable.ToList(_repository.GetByIds(GetAssociatedOrganizationIds(), options).Select(Mapper.Map<Organization, ViewOrganization>));
            return OkWithResourceLinks(PopulateOrganizationStats(organizations), options.HasMore && !NextPageExceedsSkipLimit(page, limit), page);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/admin/organizations")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        public IHttpActionResult GetForAdmins(string criteria = null, bool? paid = null, bool? suspended = null, int page = 1, int limit = 10, OrganizationSortBy sort = OrganizationSortBy.Newest) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var options = new PagingOptions { Page = page, Limit = limit };
            var organizations = Enumerable.ToList(_repository.GetByCriteria(criteria, options, sort, paid, suspended).Select(Mapper.Map<Organization, ViewOrganization>));
            return OkWithResourceLinks(PopulateOrganizationStats(organizations), options.HasMore, page);
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
            var organization = GetModel(id);
            if (organization == null)
                return NotFound();

            var viewOrganization = Mapper.Map<Organization, ViewOrganization>(organization);
            return Ok(PopulateOrganizationStats(viewOrganization));
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
        public override Task<IHttpActionResult> Delete([CommaDelimitedArray]string[] ids) {
            return base.Delete(ids);
        }

        #endregion

        [HttpGet]
        [Route("invoice/{id:minlength(10)}")]
        public IHttpActionResult GetInvoice(string id) {
            if (!Settings.Current.EnableBilling)
                return NotFound();

            if (!id.StartsWith("in_"))
                id = "in_" + id;

            StripeInvoice stripeInvoice = null;
            try {
                var invoiceService = new StripeInvoiceService();
                stripeInvoice = invoiceService.Get(id);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("An error occurred while getting the invoice: " + id).Write();
            }

            if (stripeInvoice == null || String.IsNullOrEmpty(stripeInvoice.CustomerId))
                return NotFound();

            var organization = _repository.GetByStripeCustomerId(stripeInvoice.CustomerId);
            if (organization == null || !IsInOrganization(organization.Id))
                return NotFound();

            var invoice = new Invoice {
                Id = stripeInvoice.Id.Substring(3),
                OrganizationId = organization.Id,
                OrganizationName = organization.Name,
                Date = stripeInvoice.Date.GetValueOrDefault(),
                Paid = stripeInvoice.Paid,
                Total = stripeInvoice.Total / 100.0
            };

            foreach (var line in stripeInvoice.StripeInvoiceLineItems.Data) {
                var item = new InvoiceLineItem { Amount = line.Amount / 100.0 };

                if (line.Plan != null)
                    item.Description = String.Format("Exceptionless - {0} Plan ({1}/{2})", line.Plan.Name, (line.Plan.Amount / 100.0).ToString("c"), line.Plan.Interval);
                else
                    item.Description = line.Description;

                if (stripeInvoice.PeriodStart == stripeInvoice.PeriodEnd)
                    item.Date = stripeInvoice.PeriodStart.ToShortDateString();
                else
                    item.Date = String.Format("{0} - {1}", stripeInvoice.PeriodStart.ToShortDateString(), stripeInvoice.PeriodEnd.ToShortDateString());

                invoice.Items.Add(item);
            }

            var coupon = stripeInvoice.StripeDiscount != null ? stripeInvoice.StripeDiscount.StripeCoupon : null;
            if (coupon != null) {
                double discountAmount = coupon.AmountOff ?? stripeInvoice.Subtotal * (coupon.PercentOff.GetValueOrDefault() / 100.0);
                string description = String.Format("{0} {1}", coupon.Id, coupon.PercentOff.HasValue ? String.Format("({0}% off)", coupon.PercentOff.Value) : String.Format("({0} off)", (coupon.AmountOff.GetValueOrDefault() / 100.0).ToString("C")));
               
                invoice.Items.Add(new InvoiceLineItem { Description = description, Amount = discountAmount });
            }

            return Ok(invoice);
        }

        [HttpGet]
        [Route("{id:objectid}/invoices")]
        public IHttpActionResult GetInvoices(string id, string before = null, string after = null, int limit = 12) {
            if (!Settings.Current.EnableBilling)
                return NotFound();

            var organization = GetModel(id);
            if (organization == null)
                return NotFound();

            if (String.IsNullOrWhiteSpace(organization.StripeCustomerId))
                return Ok(new List<InvoiceGridModel>());

            if (!String.IsNullOrEmpty(before) && !before.StartsWith("in_"))
                before = "in_" + before;

            if (!String.IsNullOrEmpty(after) && !after.StartsWith("in_"))
                after = "in_" + after;

            var invoiceService = new StripeInvoiceService();
            var invoices = invoiceService.List(new StripeInvoiceListOptions { CustomerId = organization.StripeCustomerId, Limit = limit + 1, EndingBefore = before, StartingAfter = after }).Select(Mapper.Map<InvoiceGridModel>).ToList();

            return OkWithResourceLinks(invoices.Take(limit).ToList(), invoices.Count > limit, i => i.Id);
        }

        [HttpGet]
        [Route("{id:objectid}/plans")]
        public IHttpActionResult GetPlans(string id) {
            var organization = GetModel(id);
            if (organization == null)
                return NotFound();

            var plans = BillingManager.Plans.ToList();
            if (!Request.IsGlobalAdmin())
                plans = plans.Where(p => !p.IsHidden || p.Id == organization.PlanId).ToList();

            var currentPlan = new BillingPlan {
                Id = organization.PlanId,
                Name = organization.PlanName,
                Description = organization.PlanDescription,
                IsHidden = false,
                Price = organization.BillingPrice,
                MaxProjects = organization.MaxProjects,
                MaxUsers = organization.MaxUsers,
                RetentionDays = organization.RetentionDays,
                MaxEventsPerMonth = organization.MaxEventsPerMonth,
                HasPremiumFeatures = organization.HasPremiumFeatures
            };

            if (plans.All(p => p.Id != organization.PlanId))
                plans.Add(currentPlan);
            else
                plans[plans.FindIndex(p => p.Id == organization.PlanId)] = currentPlan;

            return Ok(plans);
        }

        [HttpPost]
        [Route("{id:objectid}/change-plan")]
        public IHttpActionResult ChangePlan(string id, string planId, string stripeToken = null, string last4 = null, string couponId = null) {
            if (String.IsNullOrEmpty(id) || !CanAccessOrganization(id))
                return BadRequest("Invalid organization id.");

            if (!Settings.Current.EnableBilling)
                return Ok(new { Success = false, Message = "Plans cannot be changed while billing is disabled." });

            Organization organization = _repository.GetById(id);
            if (organization == null)
                return Ok(new { Success = false, Message = "Invalid OrganizationId." });

            BillingPlan plan = BillingManager.GetBillingPlan(planId);
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

                    var createCustomer = new StripeCustomerCreateOptions {
                        Card = new StripeCreditCardOptions { TokenId = stripeToken },
                        PlanId = planId,
                        Description = organization.Name,
                        Email = ExceptionlessUser.EmailAddress
                    };

                    if (!String.IsNullOrWhiteSpace(couponId))
                        createCustomer.CouponId = couponId;

                    StripeCustomer customer = customerService.Create(createCustomer);

                    organization.BillingStatus = BillingStatus.Active;
                    organization.RemoveSuspension();
                    organization.StripeCustomerId = customer.Id;
                    if (customer.StripeCardList.StripeCards.Count > 0)
                        organization.CardLast4 = customer.StripeCardList.StripeCards[0].Last4;
                } else {
                    var update = new StripeSubscriptionUpdateOptions { PlanId = planId };
                    var create = new StripeSubscriptionCreateOptions();
                    bool cardUpdated = false;

                    if (!String.IsNullOrEmpty(stripeToken)) {
                        update.Card = new StripeCreditCardOptions { TokenId = stripeToken };
                        create.Card = new StripeCreditCardOptions { TokenId = stripeToken };
                        cardUpdated = true;
                    }
                    
                    var subscription = subscriptionService.List(organization.StripeCustomerId).FirstOrDefault(s => !s.CanceledAt.HasValue);
                    if (subscription != null)
                        subscriptionService.Update(organization.StripeCustomerId, subscription.Id, update);
                    else
                        subscriptionService.Create(organization.StripeCustomerId, planId, create);

                    customerService.Update(organization.StripeCustomerId, new StripeCustomerUpdateOptions {
                        Email = ExceptionlessUser.EmailAddress
                    });

                    if (cardUpdated)
                        organization.CardLast4 = last4;

                    organization.BillingStatus = BillingStatus.Active;
                    organization.RemoveSuspension();
                }

                BillingManager.ApplyBillingPlan(organization, plan, ExceptionlessUser);
                _repository.Save(organization);

                _messagePublisher.Publish(new PlanChanged {
                    OrganizationId = organization.Id
                });
            } catch (Exception e) {
                Log.Error().Exception(e).Message("An error occurred while trying to update your billing plan: " + e.Message).Critical().Write();
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
                    _messagePublisher.Publish(new UserMembershipChanged {
                        ChangeType = ChangeType.Added,
                        UserId = user.Id,
                        OrganizationId = organization.Id
                    });
                }

                _mailer.SendAddedToOrganization(currentUser, organization, user);
            } else {
                Invite invite = organization.Invites.FirstOrDefault(i => String.Equals(i.EmailAddress, email, StringComparison.OrdinalIgnoreCase));
                if (invite == null) {
                    invite = new Invite {
                        Token = StringExtensions.GetNewToken(),
                        EmailAddress = email.ToLowerInvariant(),
                        DateAdded = DateTime.UtcNow
                    };
                    organization.Invites.Add(invite);
                    _repository.Save(organization);
                }

                _mailer.SendInvite(currentUser, organization, invite);
            }

            return Ok(new User { EmailAddress = email });
        }

        [HttpDelete]
        [Route("{id:objectid}/users/{email:minlength(1)}")]
        public IHttpActionResult RemoveUser(string id, string email) {
            if (String.IsNullOrEmpty(id) || !CanAccessOrganization(id))
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
                _messagePublisher.Publish(new UserMembershipChanged {
                    ChangeType = ChangeType.Removed,
                    UserId = user.Id,
                    OrganizationId = organization.Id
                });
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
            if (IsOrganizationNameAvailableInternal(name))
                return StatusCode(HttpStatusCode.NoContent);

            return StatusCode(HttpStatusCode.Created);
        }

        private bool IsOrganizationNameAvailableInternal(string name) {
            return !String.IsNullOrWhiteSpace(name) && !_repository.GetByIds(GetAssociatedOrganizationIds()).Any(o => o.Name.Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        protected override PermissionResult CanAdd(Organization value) {
            if (String.IsNullOrEmpty(value.Name))
                return PermissionResult.DenyWithMessage("Organization name is required.");

            if (!IsOrganizationNameAvailableInternal(value.Name))
                return PermissionResult.DenyWithMessage("A organization with this name already exists.");

            if (!_billingManager.CanAddOrganization(ExceptionlessUser))
                return PermissionResult.DenyWithPlanLimitReached("Please upgrade your plan to add an additional organization.");

            return base.CanAdd(value);
        }

        protected override Organization AddModel(Organization value) {
            BillingManager.ApplyBillingPlan(value, Settings.Current.EnableBilling ? BillingManager.FreePlan : BillingManager.UnlimitedPlan, ExceptionlessUser);

            var organization = base.AddModel(value);

            ExceptionlessUser.OrganizationIds.Add(organization.Id);
            _userRepository.Save(ExceptionlessUser, true);
            _messagePublisher.Publish(new UserMembershipChanged {
                UserId = ExceptionlessUser.Id,
                OrganizationId = organization.Id,
                ChangeType = ChangeType.Added
            });

            return organization;
        }

        protected override PermissionResult CanUpdate(Organization original, Delta<NewOrganization> changes) {
            var changed = changes.GetEntity();
            if (!IsOrganizationNameAvailableInternal(changed.Name))
                return PermissionResult.DenyWithMessage("A organization with this name already exists.");

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

        protected override async Task DeleteModels(ICollection<Organization> organizations) {
            var currentUser = ExceptionlessUser;

            foreach (var organization in organizations) {
                Log.Info().Message("User {0} deleting organization {1}.", currentUser.Id, organization.Id).Write();

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
                await base.DeleteModels(new[] { organization });
            }
        }

        protected override void CreateMaps() {
            if (Mapper.FindTypeMapFor<Organization, ViewOrganization>() == null)
                Mapper.CreateMap<Organization, ViewOrganization>().AfterMap((o, vo) => {
                    vo.IsOverHourlyLimit = o.IsOverHourlyLimit();
                    vo.IsOverMonthlyLimit = o.IsOverMonthlyLimit();
                    vo.IsOverRequestLimit = o.IsOverRequestLimit(_cacheClient, Settings.Current.ApiThrottleLimit);
                });

            if (Mapper.FindTypeMapFor<StripeInvoice, InvoiceGridModel>() == null)
                Mapper.CreateMap<StripeInvoice, InvoiceGridModel>().AfterMap((si, igm) => igm.Id = igm.Id.Substring(3));

            base.CreateMaps();
        }
    
        private ViewOrganization PopulateOrganizationStats(ViewOrganization organization) {
            return PopulateOrganizationStats(new List<ViewOrganization> { organization }).FirstOrDefault();
        }

        private List<ViewOrganization> PopulateOrganizationStats(List<ViewOrganization> organizations) {
            if (organizations.Count <= 0)
                return organizations;

            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < organizations.Count; index++) {
                if (index > 0)
                    builder.Append(" OR ");

                var organization = organizations[index];
                if (organization.RetentionDays > 0)
                    builder.AppendFormat("(organization:{0} AND (date:[now/d-{1}d TO now/d+1d}} OR last:[now/d-{1}d TO now/d+1d}}))", organization.Id, organization.RetentionDays);
                else
                    builder.AppendFormat("organization:{0}", organization.Id);
            }

            var result = _stats.GetTermsStats(DateTime.MinValue, DateTime.MaxValue, "organization_id", builder.ToString());
            foreach (var organization in organizations) {
                var organizationStats = result.Terms.FirstOrDefault(t => t.Term == organization.Id);
                organization.EventCount = organizationStats != null ? organizationStats.Total : 0;
                organization.StackCount = organizationStats != null ? organizationStats.Unique : 0;
                organization.ProjectCount = _projectRepository.GetByOrganizationId(organization.Id, useCache: true).Count;
            }

            return organizations;
        }
    }
}