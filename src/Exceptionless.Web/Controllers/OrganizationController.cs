using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Exceptionless.Web.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stripe;
using Invoice = Exceptionless.Web.Models.Invoice;
using InvoiceLineItem = Exceptionless.Web.Models.InvoiceLineItem;

#pragma warning disable 1998

namespace Exceptionless.Web.Controllers {
    [Route(API_PREFIX + "/organizations")]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    public class OrganizationController : RepositoryApiController<IOrganizationRepository, Organization, ViewOrganization, NewOrganization, NewOrganization> {
        private readonly ICacheClient _cacheClient;
        private readonly IEventRepository _eventRepository;
        private readonly IUserRepository _userRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IQueue<WorkItemData> _workItemQueue;
        private readonly BillingManager _billingManager;
        private readonly BillingPlans _plans;
        private readonly IMailer _mailer;
        private readonly IMessagePublisher _messagePublisher;
        private readonly AppOptions _options;

        public OrganizationController(
            IOrganizationRepository organizationRepository,
            ICacheClient cacheClient,
            IEventRepository eventRepository,
            IUserRepository userRepository,
            IProjectRepository projectRepository,
            IQueue<WorkItemData> workItemQueue,
            BillingManager billingManager,
            IMailer mailer,
            IMessagePublisher messagePublisher,
            IMapper mapper,
            IQueryValidator validator,
            AppOptions options,
            ILoggerFactory loggerFactory,
            BillingPlans plans) : base(organizationRepository, mapper, validator, loggerFactory) {
            _cacheClient = cacheClient;
            _eventRepository = eventRepository;
            _userRepository = userRepository;
            _projectRepository = projectRepository;
            _workItemQueue = workItemQueue;
            _billingManager = billingManager;
            _mailer = mailer;
            _messagePublisher = messagePublisher;
            _options = options;
            _plans = plans;
        }

        #region CRUD

        /// <summary>
        /// Get all
        /// </summary>
        /// <param name="mode">If no mode is set then the a light weight organization object will be returned. If the mode is set to stats than the fully populated object will be returned.</param>
        [HttpGet]
        public async Task<ActionResult<ViewOrganization>> GetAsync(string mode = null) {
            var organizations = await GetModelsAsync(GetAssociatedOrganizationIds().ToArray());
            var viewOrganizations = await MapCollectionAsync<ViewOrganization>(organizations, true);

            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "stats", StringComparison.OrdinalIgnoreCase))
                return Ok(await PopulateOrganizationStatsAsync(viewOrganizations.ToList()));

            return Ok(viewOrganizations);
        }

        [HttpGet("~/" + API_PREFIX + "/admin/organizations")]
        [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<ActionResult<IReadOnlyCollection<ViewOrganization>>> GetForAdminsAsync(string criteria = null, bool? paid = null, bool? suspended = null, string mode = null, int page = 1, int limit = 10, OrganizationSortBy sort = OrganizationSortBy.Newest) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var organizations = await _repository.GetByCriteriaAsync(criteria, o => o.PageNumber(page).PageLimit(limit), sort, paid, suspended);
            var viewOrganizations = (await MapCollectionAsync<ViewOrganization>(organizations.Documents, true)).ToList();

            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "stats", StringComparison.OrdinalIgnoreCase))
                return OkWithResourceLinks(await PopulateOrganizationStatsAsync(viewOrganizations), organizations.HasMore, page, organizations.Total);

            return OkWithResourceLinks(viewOrganizations, organizations.HasMore, page, organizations.Total);
        }

        [HttpGet("~/" + API_PREFIX + "/admin/organizations/stats")]
        [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<ActionResult<BillingPlanStats>> PlanStatsAsync() {
            return Ok(await _repository.GetBillingPlanStatsAsync());
        }

        /// <summary>
        /// Get by id
        /// </summary>
        /// <param name="id">The identifier of the organization.</param>
        /// <param name="mode">If no mode is set then the a light weight organization object will be returned. If the mode is set to stats than the fully populated object will be returned.</param>
        /// <response code="404">The organization could not be found.</response>
        [HttpGet("{id:objectid}", Name = "GetOrganizationById")]
        public async Task<ActionResult<ViewOrganization>> GetByIdAsync(string id, string mode = null) {
            var organization = await GetModelAsync(id);
            if (organization == null)
                return NotFound();

            var viewOrganization = await MapAsync<ViewOrganization>(organization, true);
            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "stats", StringComparison.OrdinalIgnoreCase))
                return Ok(await PopulateOrganizationStatsAsync(viewOrganization));

            return Ok(viewOrganization);
        }

        /// <summary>
        /// Create
        /// </summary>
        /// <param name="organization">The organization.</param>
        /// <returns></returns>
        /// <response code="400">An error occurred while creating the organization.</response>
        /// <response code="409">The organization already exists.</response>
        [HttpPost]
        [Consumes("application/json")]
        public Task<ActionResult<ViewOrganization>> PostAsync(NewOrganization organization) {
            return PostImplAsync(organization);
        }

        /// <summary>
        /// Update
        /// </summary>
        /// <param name="id">The identifier of the organization.</param>
        /// <param name="changes">The changes</param>
        /// <response code="400">An error occurred while updating the organization.</response>
        /// <response code="404">The organization could not be found.</response>
        [HttpPatch]
        [HttpPut]
        [Consumes("application/json")]
        [Route("{id:objectid}")]
        public Task<ActionResult<ViewOrganization>> PatchAsync(string id, Delta<NewOrganization> changes) {
            return PatchImplAsync(id, changes);
        }

        /// <summary>
        /// Remove
        /// </summary>
        /// <param name="ids">A comma delimited list of organization identifiers.</param>
        /// <response code="204">No Content.</response>
        /// <response code="400">One or more validation errors occurred.</response>
        /// <response code="404">One or more organizations were not found.</response>
        /// <response code="500">An error occurred while deleting one or more organizations.</response>
        [HttpDelete]
        [Route("{ids:objectids}")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        public Task<ActionResult<WorkInProgressResult>> DeleteAsync(string ids) {
            return DeleteImplAsync(ids.FromDelimitedString());
        }

        #endregion

        /// <summary>
        /// Get invoice
        /// </summary>
        /// <param name="id">The identifier of the invoice.</param>
        /// <response code="404">The invoice was not found.</response>
        [HttpGet]
        [Route("invoice/{id:minlength(10)}")]
        public async Task<ActionResult<Invoice>> GetInvoiceAsync(string id) {
            if (!_options.StripeOptions.EnableBilling)
                return NotFound();

            if (!id.StartsWith("in_"))
                id = "in_" + id;

            Stripe.Invoice stripeInvoice = null;
            try {
                var client = new StripeClient(_options.StripeOptions.StripeApiKey);
                var invoiceService = new InvoiceService(client);
                stripeInvoice = await invoiceService.GetAsync(id);
            } catch (Exception ex) {
                using (_logger.BeginScope(new ExceptionlessState().Tag("Invoice").Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).SetHttpContext(HttpContext)))
                    _logger.LogError(ex, "An error occurred while getting the invoice: {InvoiceId}", id);
            }

            if (String.IsNullOrEmpty(stripeInvoice?.CustomerId))
                return NotFound();

            var organization = await _repository.GetByStripeCustomerIdAsync(stripeInvoice.CustomerId);
            if (organization == null || !CanAccessOrganization(organization.Id))
                return NotFound();

            var invoice = new Invoice {
                Id = stripeInvoice.Id.Substring(3),
                OrganizationId = organization.Id,
                OrganizationName = organization.Name,
                Date = stripeInvoice.Created,
                Paid = stripeInvoice.Paid,
                Total = stripeInvoice.Total / 100.0m
            };

            foreach (var line in stripeInvoice.Lines.Data) {
                var item = new InvoiceLineItem { Amount = line.Amount / 100.0m };

                if (line.Plan != null) {
                    string planName = line.Plan.Nickname ?? _billingManager.GetBillingPlan(line.Plan.Id)?.Name;
                    item.Description = $"Exceptionless - {planName} Plan ({(line.Plan.Amount / 100.0):c}/{line.Plan.Interval})";
                } else {
                    item.Description = line.Description;
                }

                item.Date = $"{(line.Period.Start ?? stripeInvoice.PeriodStart).ToShortDateString()} - {(line.Period.End ?? stripeInvoice.PeriodEnd).ToShortDateString()}";
                invoice.Items.Add(item);
            }

            var coupon = stripeInvoice.Discount?.Coupon;
            if (coupon != null) {
                if (coupon.AmountOff.HasValue) {
                    decimal discountAmount = coupon.AmountOff.GetValueOrDefault() / 100.0m;
                    string description = $"{coupon.Id} ({discountAmount.ToString("C")} off)";
                    invoice.Items.Add(new InvoiceLineItem { Description = description, Amount = discountAmount });
                } else {
                    decimal discountAmount = (stripeInvoice.Subtotal / 100.0m) * (coupon.PercentOff.GetValueOrDefault() / 100.0m);
                    string description = $"{coupon.Id} ({coupon.PercentOff.GetValueOrDefault()}% off)";
                    invoice.Items.Add(new InvoiceLineItem { Description = description, Amount = discountAmount });
                }
            }

            return Ok(invoice);
        }

        /// <summary>
        /// Get invoices
        /// </summary>
        /// <param name="id">The identifier of the organization.</param>
        /// <param name="before">A cursor for use in pagination. before is an object ID that defines your place in the list. For instance, if you make a list request and receive 100 objects, starting with obj_bar, your subsequent call can include before=obj_bar in order to fetch the previous page of the list.</param>
        /// <param name="after">A cursor for use in pagination. after is an object ID that defines your place in the list. For instance, if you make a list request and receive 100 objects, ending with obj_foo, your subsequent call can include after=obj_foo in order to fetch the next page of the list.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="404">The organization was not found.</response>
        [HttpGet]
        [Route("{id:objectid}/invoices")]
        public async Task<ActionResult<IReadOnlyCollection<InvoiceGridModel>>> GetInvoicesAsync(string id, string before = null, string after = null, int limit = 12) {
            if (!_options.StripeOptions.EnableBilling)
                return NotFound();

            var organization = await GetModelAsync(id);
            if (organization == null)
                return NotFound();

            if (String.IsNullOrWhiteSpace(organization.StripeCustomerId))
                return Ok(new List<InvoiceGridModel>());

            if (!String.IsNullOrEmpty(before) && !before.StartsWith("in_"))
                before = "in_" + before;

            if (!String.IsNullOrEmpty(after) && !after.StartsWith("in_"))
                after = "in_" + after;

            var client = new StripeClient(_options.StripeOptions.StripeApiKey);
            var invoiceService = new InvoiceService(client);
            var invoiceOptions = new InvoiceListOptions { Customer = organization.StripeCustomerId, Limit = limit + 1, EndingBefore = before, StartingAfter = after };
            var invoices = (await MapCollectionAsync<InvoiceGridModel>(await invoiceService.ListAsync(invoiceOptions), true)).ToList();
            return OkWithResourceLinks(invoices.Take(limit).ToList(), invoices.Count > limit, i => i.Id);
        }

        /// <summary>
        /// Get plans
        /// </summary>
        /// <remarks>
        /// Gets available plans for a specific organization.
        /// </remarks>
        /// <param name="id">The identifier of the organization.</param>
        /// <response code="404">The organization was not found.</response>
        [HttpGet]
        [Route("{id:objectid}/plans")]
        public async Task<ActionResult<IReadOnlyCollection<BillingPlan>>> GetPlansAsync(string id) {
            var organization = await GetModelAsync(id);
            if (organization == null)
                return NotFound();

            var plans = _plans.Plans;
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

        /// <summary>
        /// Change plan
        /// </summary>
        /// <remarks>
        /// Upgrades or downgrades the organizations plan.
        /// </remarks>
        /// <param name="id">The identifier of the organization.</param>
        /// <param name="planId">The identifier of the plan.</param>
        /// <param name="stripeToken">The token returned from the stripe service.</param>
        /// <param name="last4">The last four numbers of the card.</param>
        /// <param name="couponId">The coupon id.</param>
        /// <response code="404">The organization was not found.</response>
        [HttpPost]
        [Consumes("application/json")]
        [Route("{id:objectid}/change-plan")]
        public async Task<ActionResult<ChangePlanResult>> ChangePlanAsync(string id, string planId, string stripeToken = null, string last4 = null, string couponId = null) {
            if (String.IsNullOrEmpty(id) || !CanAccessOrganization(id))
                return NotFound();

            if (!_options.StripeOptions.EnableBilling)
                return Ok(ChangePlanResult.FailWithMessage("Plans cannot be changed while billing is disabled."));

            var organization = await GetModelAsync(id, false);
            if (organization == null)
                return Ok(ChangePlanResult.FailWithMessage("Invalid OrganizationId."));

            var plan = _billingManager.GetBillingPlan(planId);
            if (plan == null)
                return Ok(ChangePlanResult.FailWithMessage("Invalid PlanId."));

            if (String.Equals(organization.PlanId, plan.Id) && String.Equals(_plans.FreePlan.Id, plan.Id))
                return Ok(ChangePlanResult.SuccessWithMessage("Your plan was not changed as you were already on the free plan."));

            // Only see if they can downgrade a plan if the plans are different.
            if (!String.Equals(organization.PlanId, plan.Id)) {
                var result = await _billingManager.CanDownGradeAsync(organization, plan, CurrentUser);
                if (!result.Success)
                    return Ok(result);
            }

            var client = new StripeClient(_options.StripeOptions.StripeApiKey);
            var customerService = new CustomerService(client);
            var subscriptionService = new SubscriptionService(client);

            try {
                // If they are on a paid plan and then downgrade to a free plan then cancel their stripe subscription.
                if (!String.Equals(organization.PlanId, _plans.FreePlan.Id) && String.Equals(plan.Id, _plans.FreePlan.Id)) {
                    if (!String.IsNullOrEmpty(organization.StripeCustomerId)) {
                        var subs = await subscriptionService.ListAsync(new SubscriptionListOptions { Customer = organization.StripeCustomerId });
                        foreach (var sub in subs.Where(s => !s.CanceledAt.HasValue))
                            await subscriptionService.CancelAsync(sub.Id, new SubscriptionCancelOptions());
                    }

                    organization.BillingStatus = BillingStatus.Trialing;
                    organization.RemoveSuspension();
                } else if (String.IsNullOrEmpty(organization.StripeCustomerId)) {
                    if (String.IsNullOrEmpty(stripeToken))
                        return Ok(ChangePlanResult.FailWithMessage("Billing information was not set."));

                    organization.SubscribeDate = SystemClock.UtcNow;

                    var createCustomer = new CustomerCreateOptions {
                        Source = stripeToken,
                        Plan = planId,
                        Description = organization.Name,
                        Email = CurrentUser.EmailAddress
                    };

                    if (!String.IsNullOrWhiteSpace(couponId))
                        createCustomer.Coupon = couponId;

                    var customer = await customerService.CreateAsync(createCustomer);

                    organization.BillingStatus = BillingStatus.Active;
                    organization.RemoveSuspension();
                    organization.StripeCustomerId = customer.Id;
                    if (customer.Sources.Data.Count > 0)
                        organization.CardLast4 = (customer.Sources.Data.First() as Card)?.Last4;
                } else {
                    var update = new SubscriptionUpdateOptions { Items =  new List<SubscriptionItemOptions>() };
                    var create = new SubscriptionCreateOptions { Customer = organization.StripeCustomerId, Items = new List<SubscriptionItemOptions>() };
                    bool cardUpdated = false;

                    var customerUpdateOptions = new CustomerUpdateOptions { Description = organization.Name, Email = CurrentUser.EmailAddress };
                    if (!String.IsNullOrEmpty(stripeToken)) {
                        customerUpdateOptions.Source = stripeToken;
                        cardUpdated = true;
                    }

                    await customerService.UpdateAsync(organization.StripeCustomerId, customerUpdateOptions);

                    var subscriptionList = await subscriptionService.ListAsync(new SubscriptionListOptions { Customer = organization.StripeCustomerId });
                    var subscription = subscriptionList.FirstOrDefault(s => !s.CanceledAt.HasValue);
                    if (subscription != null) {
                        update.Items.Add(new SubscriptionItemOptions { Id = subscription.Items.Data[0].Id, Plan = planId });
                        await subscriptionService.UpdateAsync(subscription.Id, update);
                    } else {
                        create.Items.Add(new SubscriptionItemOptions { Plan = planId });
                        await subscriptionService.CreateAsync(create);
                    }

                    if (cardUpdated)
                        organization.CardLast4 = last4;

                    organization.BillingStatus = BillingStatus.Active;
                    organization.RemoveSuspension();
                }

                _billingManager.ApplyBillingPlan(organization, plan, CurrentUser);
                await _repository.SaveAsync(organization, o => o.Cache());
                await _messagePublisher.PublishAsync(new PlanChanged { OrganizationId = organization.Id });
            } catch (Exception ex) {
                using (_logger.BeginScope(new ExceptionlessState().Tag("Change Plan").Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).SetHttpContext(HttpContext)))
                    _logger.LogCritical(ex, "An error occurred while trying to update your billing plan: {Message}", ex.Message);

                return Ok(ChangePlanResult.FailWithMessage(ex.Message));
            }

            return Ok(new ChangePlanResult { Success = true });
        }

        /// <summary>
        /// Add user
        /// </summary>
        /// <param name="id">The identifier of the organization.</param>
        /// <param name="email">The email address of the user you wish to add to your organization.</param>
        /// <response code="404">The organization was not found.</response>
        /// <response code="426">Please upgrade your plan to add an additional user.</response>
        [HttpPost]
        [Route("{id:objectid}/users/{email:minlength(1)}")]
        public async Task<ActionResult<User>> AddUserAsync(string id, string email) {
            if (String.IsNullOrEmpty(id) || !CanAccessOrganization(id) || String.IsNullOrEmpty(email))
                return NotFound();

            var organization = await GetModelAsync(id);
            if (organization == null)
                return NotFound();

            if (!await _billingManager.CanAddUserAsync(organization))
                return PlanLimitReached("Please upgrade your plan to add an additional user.");

            var user = await _userRepository.GetByEmailAddressAsync(email);
            if (user != null) {
                if (!user.OrganizationIds.Contains(organization.Id)) {
                    user.OrganizationIds.Add(organization.Id);
                    await _userRepository.SaveAsync(user, o => o.Cache());
                    await _messagePublisher.PublishAsync(new UserMembershipChanged {
                        ChangeType = ChangeType.Added,
                        UserId = user.Id,
                        OrganizationId = organization.Id
                    });
                }

                await _mailer.SendOrganizationAddedAsync(CurrentUser, organization, user);
            } else {
                var invite = organization.Invites.FirstOrDefault(i => String.Equals(i.EmailAddress, email, StringComparison.OrdinalIgnoreCase));
                if (invite == null) {
                    invite = new Invite {
                        Token = StringExtensions.GetNewToken(),
                        EmailAddress = email.ToLowerInvariant(),
                        DateAdded = SystemClock.UtcNow
                    };
                    organization.Invites.Add(invite);
                    await _repository.SaveAsync(organization, o => o.Cache());
                }

                await _mailer.SendOrganizationInviteAsync(CurrentUser, organization, invite);
            }

            return Ok(new User { EmailAddress = email });
        }

        /// <summary>
        /// Remove user
        /// </summary>
        /// <param name="id">The identifier of the organization.</param>
        /// <param name="email">The email address of the user you wish to remove from your organization.</param>
        /// <response code="400">The error occurred while removing the user from your organization</response>
        /// <response code="404">The organization was not found.</response>
        [HttpDelete]
        [Route("{id:objectid}/users/{email:minlength(1)}")]
        public async Task<IActionResult> RemoveUserAsync(string id, string email) {
            var organization = await GetModelAsync(id, false);
            if (organization == null)
                return NotFound();

            var user = await _userRepository.GetByEmailAddressAsync(email);
            if (user == null || !user.OrganizationIds.Contains(id)) {
                var invite = organization.Invites.FirstOrDefault(i => String.Equals(i.EmailAddress, email, StringComparison.OrdinalIgnoreCase));
                if (invite == null)
                    return Ok();

                organization.Invites.Remove(invite);
                await _repository.SaveAsync(organization, o => o.Cache());
            } else {
                if (!user.OrganizationIds.Contains(organization.Id))
                    return BadRequest();

                if ((await _userRepository.GetByOrganizationIdAsync(organization.Id)).Total == 1)
                    return BadRequest("An organization must contain at least one user.");

                var projects = (await _projectRepository.GetByOrganizationIdAsync(organization.Id)).Documents.Where(p => p.NotificationSettings.ContainsKey(user.Id)).ToList();
                if (projects.Count > 0) {
                    foreach (var project in projects)
                        project.NotificationSettings.Remove(user.Id);

                    await _projectRepository.SaveAsync(projects);
                }

                user.OrganizationIds.Remove(organization.Id);
                await _userRepository.SaveAsync(user, o => o.Cache());
                await _messagePublisher.PublishAsync(new UserMembershipChanged {
                    ChangeType = ChangeType.Removed,
                    UserId = user.Id,
                    OrganizationId = organization.Id
                });
            }

            return Ok();
        }

        [HttpPost]
        [Route("{id:objectid}/suspend")]
        [Consumes("application/json")]
        [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> SuspendAsync(string id, SuspensionCode code, string notes = null) {
            var organization = await GetModelAsync(id, false);
            if (organization == null)
                return NotFound();

            organization.IsSuspended = true;
            organization.SuspensionDate = SystemClock.UtcNow;
            organization.SuspendedByUserId = CurrentUser.Id;
            organization.SuspensionCode = code;
            organization.SuspensionNotes = notes;
            await _repository.SaveAsync(organization, o => o.Cache());

            return Ok();
        }

        [HttpDelete]
        [Route("{id:objectid}/suspend")]
        [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> UnsuspendAsync(string id) {
            var organization = await GetModelAsync(id, false);
            if (organization == null)
                return NotFound();

            organization.IsSuspended = false;
            organization.SuspensionDate = null;
            organization.SuspendedByUserId = null;
            organization.SuspensionCode = null;
            organization.SuspensionNotes = null;
            await _repository.SaveAsync(organization, o => o.Cache());

            return Ok();
        }

        /// <summary>
        /// Add custom data
        /// </summary>
        /// <param name="id">The identifier of the organization.</param>
        /// <param name="key">The key name of the data object.</param>
        /// <param name="value">Any string value.</param>
        /// <response code="404">The organization was not found.</response>
        [HttpPost]
        [Consumes("application/json")]
        [Route("{id:objectid}/data/{key:minlength(1)}")]
        public async Task<IActionResult> PostDataAsync(string id, string key, ValueFromBody<string> value) {
            if (String.IsNullOrWhiteSpace(key) || String.IsNullOrWhiteSpace(value?.Value) || key.StartsWith("-"))
                return BadRequest();

            var organization = await GetModelAsync(id, false);
            if (organization == null)
                return NotFound();

            organization.Data[key.Trim()] = value.Value.Trim();
            await _repository.SaveAsync(organization, o => o.Cache());

            return Ok();
        }

        /// <summary>
        /// Remove custom data
        /// </summary>
        /// <param name="id">The identifier of the organization.</param>
        /// <param name="key">The key name of the data object.</param>
        /// <response code="404">The organization was not found.</response>
        [HttpDelete]
        [Route("{id:objectid}/data/{key:minlength(1)}")]
        public async Task<IActionResult> DeleteDataAsync(string id, string key) {
            var organization = await GetModelAsync(id, false);
            if (organization == null)
                return NotFound();

            if (organization.Data.Remove(key))
                await _repository.SaveAsync(organization, o => o.Cache());

            return Ok();
        }

        /// <summary>
        /// Check for unique name
        /// </summary>
        /// <param name="name">The organization name to check.</param>
        /// <response code="201">The organization name is available.</response>
        /// <response code="204">The organization name is not available.</response>
        [HttpGet]
        [Route("check-name")]
        public async Task<IActionResult> IsNameAvailableAsync(string name) {
            if (await IsOrganizationNameAvailableInternalAsync(name))
                return StatusCode(StatusCodes.Status204NoContent);

            return StatusCode(StatusCodes.Status201Created);
        }

        private async Task<bool> IsOrganizationNameAvailableInternalAsync(string name) {
            if (String.IsNullOrWhiteSpace(name))
                return false;

            string decodedName = Uri.UnescapeDataString(name).Trim().ToLowerInvariant();
            var results = await _repository.GetByIdsAsync(GetAssociatedOrganizationIds().ToArray(), o => o.Cache());
            return !results.Any(o => String.Equals(o.Name.Trim().ToLowerInvariant(), decodedName, StringComparison.OrdinalIgnoreCase));
        }

        protected override async Task<PermissionResult> CanAddAsync(Organization value) {
            if (String.IsNullOrEmpty(value.Name))
                return PermissionResult.DenyWithMessage("Organization name is required.");

            if (!await IsOrganizationNameAvailableInternalAsync(value.Name))
                return PermissionResult.DenyWithMessage("A organization with this name already exists.");

            if (!await _billingManager.CanAddOrganizationAsync(CurrentUser))
                return PermissionResult.DenyWithPlanLimitReached("Please upgrade your plan to add an additional organization.");

            return await base.CanAddAsync(value);
        }

        protected override async Task<Organization> AddModelAsync(Organization value) {
            _billingManager.ApplyBillingPlan(value, _options.StripeOptions.EnableBilling ? _plans.FreePlan : _plans.UnlimitedPlan, CurrentUser);

            var organization = await base.AddModelAsync(value);

            CurrentUser.OrganizationIds.Add(organization.Id);
            await _userRepository.SaveAsync(CurrentUser, o => o.Cache());
            await _messagePublisher.PublishAsync(new UserMembershipChanged {
                UserId = CurrentUser.Id,
                OrganizationId = organization.Id,
                ChangeType = ChangeType.Added
            });

            return organization;
        }

        protected override async Task<PermissionResult> CanUpdateAsync(Organization original, Delta<NewOrganization> changes) {
            var changed = changes.GetEntity();
            if (!await IsOrganizationNameAvailableInternalAsync(changed.Name))
                return PermissionResult.DenyWithMessage("A organization with this name already exists.");

            return await base.CanUpdateAsync(original, changes);
        }

        protected override async Task<PermissionResult> CanDeleteAsync(Organization value) {
            if (!String.IsNullOrEmpty(value.StripeCustomerId) && !User.IsInRole(AuthorizationRoles.GlobalAdmin))
                return PermissionResult.DenyWithMessage("An organization cannot be deleted if it has a subscription.", value.Id);

            var projects = (await _projectRepository.GetByOrganizationIdAsync(value.Id)).Documents.ToList();
            if (!User.IsInRole(AuthorizationRoles.GlobalAdmin) && projects.Any())
                return PermissionResult.DenyWithMessage("An organization cannot be deleted if it contains any projects.", value.Id);

            return await base.CanDeleteAsync(value);
        }

        protected override async Task<IEnumerable<string>> DeleteModelsAsync(ICollection<Organization> organizations) {
            var workItems = new List<string>();
            foreach (var organization in organizations) {
                using (_logger.BeginScope(new ExceptionlessState().Organization(organization.Id).Tag("Delete").Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).SetHttpContext(HttpContext)))
                    _logger.LogInformation("User {user} deleting organization {organization}.", CurrentUser.Id, organization.Id);

                workItems.Add(await _workItemQueue.EnqueueAsync(new RemoveOrganizationWorkItem {
                    OrganizationId = organization.Id,
                    CurrentUserId = CurrentUser.Id,
                    IsGlobalAdmin = User.IsInRole(AuthorizationRoles.GlobalAdmin)
                }));
            }

            return workItems;
        }

        protected override async Task AfterResultMapAsync<TDestination>(ICollection<TDestination> models) {
            await base.AfterResultMapAsync(models);

            var viewOrganizations = models.OfType<ViewOrganization>().ToList();
            foreach (var viewOrganization in viewOrganizations) {
                var usageRetention = SystemClock.UtcNow.SubtractYears(1).StartOfMonth();
                viewOrganization.Usage = viewOrganization.Usage.Where(u => u.Date > usageRetention).ToList();
                viewOrganization.OverageHours = viewOrganization.OverageHours.Where(u => u.Date > usageRetention).ToList();
                viewOrganization.IsOverRequestLimit = await OrganizationExtensions.IsOverRequestLimitAsync(viewOrganization.Id, _cacheClient, _options.ApiThrottleLimit);
            }
        }

        private async Task<ViewOrganization> PopulateOrganizationStatsAsync(ViewOrganization organization) {
            return (await PopulateOrganizationStatsAsync(new List<ViewOrganization> { organization })).FirstOrDefault();
        }

        private async Task<List<ViewOrganization>> PopulateOrganizationStatsAsync(List<ViewOrganization> viewOrganizations) {
            if (viewOrganizations.Count <= 0)
                return viewOrganizations;

            int maximumRetentionDays = _options.MaximumRetentionDays;
            var organizations = viewOrganizations.Select(o => new Organization { Id = o.Id, CreatedUtc = o.CreatedUtc, RetentionDays = o.RetentionDays }).ToList();
            var sf = new AppFilter(organizations);
            var systemFilter = new RepositoryQuery<PersistentEvent>().AppFilter(sf).DateRange(organizations.GetRetentionUtcCutoff(maximumRetentionDays), SystemClock.UtcNow, (PersistentEvent e) => e.Date).Index(organizations.GetRetentionUtcCutoff(maximumRetentionDays), SystemClock.UtcNow);
            var result = await _eventRepository.CountBySearchAsync(systemFilter, null, $"terms:(organization_id~{viewOrganizations.Count} cardinality:stack_id)");
            foreach (var organization in viewOrganizations) {
                var organizationStats = result.Aggregations.Terms<string>("terms_organization_id")?.Buckets.FirstOrDefault(t => t.Key == organization.Id);
                organization.EventCount = organizationStats?.Total ?? 0;
                organization.StackCount = (long?)organizationStats?.Aggregations.Cardinality("cardinality_stack_id")?.Value ?? 0;
                organization.ProjectCount = await _projectRepository.GetCountByOrganizationIdAsync(organization.Id);
            }

            return viewOrganizations;
        }
    }
}
