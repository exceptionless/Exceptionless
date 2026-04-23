using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Services;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Mapping;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Stripe;
using DataDictionary = Exceptionless.Core.Models.DataDictionary;
using Invoice = Exceptionless.Web.Models.Invoice;
using InvoiceLineItem = Exceptionless.Web.Models.InvoiceLineItem;

namespace Exceptionless.Web.Controllers;

[Route(API_PREFIX + "/organizations")]
[Authorize(Policy = AuthorizationRoles.UserPolicy)]
public class OrganizationController : RepositoryApiController<IOrganizationRepository, Organization, ViewOrganization, NewOrganization, NewOrganization>
{
    private readonly OrganizationService _organizationService;
    private readonly ICacheClient _cacheClient;
    private readonly IEventRepository _eventRepository;
    private readonly IUserRepository _userRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly BillingManager _billingManager;
    private readonly UsageService _usageService;
    private readonly BillingPlans _plans;
    private readonly IMailer _mailer;
    private readonly IMessagePublisher _messagePublisher;
    private readonly AppOptions _options;

    public OrganizationController(
        OrganizationService organizationService,
        IOrganizationRepository organizationRepository,
        ICacheClient cacheClient,
        IEventRepository eventRepository,
        IUserRepository userRepository,
        IProjectRepository projectRepository,
        BillingManager billingManager,
        BillingPlans plans,
        UsageService usageService,
        IMailer mailer,
        IMessagePublisher messagePublisher,
        ApiMapper mapper,
        IAppQueryValidator validator,
        AppOptions options,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory) : base(organizationRepository, mapper, validator, timeProvider, loggerFactory)
    {
        _organizationService = organizationService;
        _cacheClient = cacheClient;
        _eventRepository = eventRepository;
        _userRepository = userRepository;
        _projectRepository = projectRepository;
        _billingManager = billingManager;
        _plans = plans;
        _usageService = usageService;
        _mailer = mailer;
        _messagePublisher = messagePublisher;
        _options = options;
    }

    // Mapping implementations
    protected override Organization MapToModel(NewOrganization newModel) => _mapper.MapToOrganization(newModel);
    protected override ViewOrganization MapToViewModel(Organization model) => _mapper.MapToViewOrganization(model);
    protected override List<ViewOrganization> MapToViewModels(IEnumerable<Organization> models) => _mapper.MapToViewOrganizations(models);

    /// <summary>
    /// Get all
    /// </summary>
    /// <param name="mode">If no mode is set then a lightweight organization object will be returned. If the mode is set to stats than the fully populated object will be returned.</param>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ViewOrganization>>> GetAllAsync(string? mode = null)
    {
        var organizations = await GetModelsAsync(GetAssociatedOrganizationIds().ToArray());
        var viewOrganizations = MapToViewModels(organizations);
        await AfterResultMapAsync(viewOrganizations);

        if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "stats", StringComparison.OrdinalIgnoreCase))
            return Ok(await PopulateOrganizationStatsAsync(viewOrganizations));

        return Ok(viewOrganizations);
    }

    [HttpGet("~/" + API_PREFIX + "/admin/organizations")]
    [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<ActionResult<IReadOnlyCollection<ViewOrganization>>> GetForAdminsAsync(string? criteria = null, bool? paid = null, bool? suspended = null, string? mode = null, int page = 1, int limit = 10, OrganizationSortBy sort = OrganizationSortBy.Newest)
    {
        page = GetPage(page);
        limit = GetLimit(limit);
        var organizations = await _repository.GetByCriteriaAsync(criteria, o => o.PageNumber(page).PageLimit(limit), sort, paid, suspended);
        var viewOrganizations = MapToViewModels(organizations.Documents);
        await AfterResultMapAsync(viewOrganizations);

        if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "stats", StringComparison.OrdinalIgnoreCase))
            return OkWithResourceLinks(await PopulateOrganizationStatsAsync(viewOrganizations), organizations.HasMore, page, organizations.Total);

        return OkWithResourceLinks(viewOrganizations, organizations.HasMore, page, organizations.Total);
    }

    [HttpGet("~/" + API_PREFIX + "/admin/organizations/stats")]
    [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<ActionResult<BillingPlanStats>> PlanStatsAsync()
    {
        return Ok(await _repository.GetBillingPlanStatsAsync());
    }

    /// <summary>
    /// Get by id
    /// </summary>
    /// <param name="id">The identifier of the organization.</param>
    /// <param name="mode">If no mode is set then the a lightweight organization object will be returned. If the mode is set to stats than the fully populated object will be returned.</param>
    /// <response code="404">The organization could not be found.</response>
    [HttpGet("{id:objectid}", Name = "GetOrganizationById")]
    public async Task<ActionResult<ViewOrganization>> GetAsync(string id, string? mode = null)
    {
        var organization = await GetModelAsync(id);
        if (organization is null)
            return NotFound();

        var viewOrganization = MapToViewModel(organization);
        await AfterResultMapAsync<ViewOrganization>([viewOrganization]);

        if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "stats", StringComparison.OrdinalIgnoreCase))
            return Ok(await PopulateOrganizationStatsAsync(viewOrganization));

        return Ok(viewOrganization);
    }

    /// <summary>
    /// Create
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <response code="400">An error occurred while creating the organization.</response>
    /// <response code="409">The organization already exists.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ViewOrganization>(StatusCodes.Status201Created)]
    public Task<ActionResult<ViewOrganization>> PostAsync(NewOrganization organization)
    {
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
    public Task<ActionResult<ViewOrganization>> PatchAsync(string id, Delta<NewOrganization> changes)
    {
        return PatchImplAsync(id, changes);
    }

    /// <summary>
    /// Remove
    /// </summary>
    /// <param name="ids">A comma-delimited list of organization identifiers.</param>
    /// <response code="202">Accepted</response>
    /// <response code="400">One or more validation errors occurred.</response>
    /// <response code="404">One or more organizations were not found.</response>
    /// <response code="500">An error occurred while deleting one or more organizations.</response>
    [HttpDelete]
    [Route("{ids:objectids}")]
    [ProducesResponseType<WorkInProgressResult>(StatusCodes.Status202Accepted)]
    public Task<ActionResult<WorkInProgressResult>> DeleteAsync(string ids)
    {
        return DeleteImplAsync(ids.FromDelimitedString());
    }

    protected override async Task<IEnumerable<string>> DeleteModelsAsync(ICollection<Organization> organizations)
    {
        var user = CurrentUser;
        foreach (var organization in organizations)
        {
            using var _ = _logger.BeginScope(new ExceptionlessState().Organization(organization.Id).Tag("Delete").Identity(user.EmailAddress).Property("User", user).SetHttpContext(HttpContext));
            _logger.UserDeletingOrganization(user.Id, organization.Name, organization.Id);
            await _organizationService.SoftDeleteOrganizationAsync(organization, user.Id);
        }

        return [];
    }

    /// <summary>
    /// Get invoice
    /// </summary>
    /// <param name="id">The identifier of the invoice.</param>
    /// <response code="404">The invoice was not found.</response>
    [HttpGet]
    [Route("invoice/{id:minlength(10)}")]
    public async Task<ActionResult<Invoice>> GetInvoiceAsync(string id)
    {
        if (!_options.StripeOptions.EnableBilling)
            return NotFound();

        using var _ = _logger.BeginScope(new ExceptionlessState().Tag("Invoice").Identity(CurrentUser.EmailAddress)
                   .Property("User", CurrentUser).SetHttpContext(HttpContext));

        if (!id.StartsWith("in_"))
            id = "in_" + id;

        Stripe.Invoice? stripeInvoice = null;
        var client = new StripeClient(_options.StripeOptions.StripeApiKey);

        try
        {
            var invoiceService = new InvoiceService(client);
            stripeInvoice = await invoiceService.GetAsync(id);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "An error occurred while getting the invoice: {InvoiceId}", id);
        }

        if (String.IsNullOrEmpty(stripeInvoice?.CustomerId))
            return NotFound();

        var organization = await _repository.GetByStripeCustomerIdAsync(stripeInvoice.CustomerId);
        if (organization is null || !CanAccessOrganization(organization.Id))
            return NotFound();

        var invoice = new Invoice
        {
            Id = stripeInvoice.Id.Substring(3),
            OrganizationId = organization.Id,
            OrganizationName = organization.Name,
            Date = stripeInvoice.Created,
            Paid = String.Equals(stripeInvoice.Status, "paid", StringComparison.OrdinalIgnoreCase),
            Total = stripeInvoice.Total / 100.0m
        };

        var priceService = new PriceService(client);
        var priceCache = new Dictionary<string, Stripe.Price>(StringComparer.Ordinal);
        foreach (var line in stripeInvoice.Lines.Data)
        {
            var item = new InvoiceLineItem { Amount = line.Amount / 100.0m, Description = line.Description };

            // In Stripe.net 51.x, PriceDetails.Price changed from string to ExpandableField<Price>;
            // use .PriceId for the string ID. Fetch full Price object from Stripe to get nickname, interval, and amount.
            var priceId = line.Pricing?.PriceDetails?.PriceId;
            if (!String.IsNullOrEmpty(priceId))
            {
                try
                {
                    if (!priceCache.TryGetValue(priceId, out var price))
                    {
                        price = await priceService.GetAsync(priceId);
                        priceCache[priceId] = price;
                    }

                    var billingPlan = _billingManager.GetBillingPlan(price.Id);
                    if (billingPlan is null && !String.IsNullOrEmpty(price.LookupKey))
                        billingPlan = _billingManager.GetBillingPlan(price.LookupKey);

                    // Find the matching billing plan by checking multiple identifiers:
                    // 1. Price ID (e.g., "EX_SMALL" if using custom IDs)
                    // 2. Lookup key (alternative identifier set in Stripe)
                    // 3. Nickname for display fallback
                    string planName = billingPlan?.Name ?? price.Nickname ?? price.Id;
                    string interval = price.Recurring?.Interval ?? "one-time";
                    decimal unitAmountCents = line.Pricing?.UnitAmountDecimal ?? price.UnitAmount ?? 0;
                    item.Description = $"Exceptionless - {planName} Plan ({unitAmountCents / 100.0m:c}/{interval})";
                }
                catch (StripeException ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch price details for price: {PriceId}", priceId);
                }
            }

            var periodStart = line.Period.Start >= DateTime.MinValue ? line.Period.Start : stripeInvoice.PeriodStart;
            var periodEnd = line.Period.End >= DateTime.MinValue ? line.Period.End : stripeInvoice.PeriodEnd;
            item.Date = $"{periodStart.ToShortDateString()} - {periodEnd.ToShortDateString()}";
            invoice.Items.Add(item);
        }

        // In Stripe.net 50.x, Discount was replaced with Discounts collection
        // and Discount.Coupon was replaced with Discount.Source.Coupon
        var coupon = stripeInvoice.Discounts?.FirstOrDefault(d => d.Deleted is not true)?.Source?.Coupon;
        if (coupon is not null)
        {
            if (coupon.AmountOff.HasValue)
            {
                decimal discountAmount = coupon.AmountOff.GetValueOrDefault() / 100.0m;
                string description = $"{coupon.Id} ({discountAmount:C} off)";
                invoice.Items.Add(new InvoiceLineItem { Description = description, Amount = discountAmount });
            }
            else
            {
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
    public async Task<ActionResult<IReadOnlyCollection<InvoiceGridModel>>> GetInvoicesAsync(string id, string? before = null, string? after = null, int limit = 12)
    {
        if (!_options.StripeOptions.EnableBilling)
            return NotFound();

        var organization = await GetModelAsync(id);
        if (organization is null)
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
        var invoices = _mapper.MapToInvoiceGridModels(await invoiceService.ListAsync(invoiceOptions));
        return OkWithResourceLinks(invoices.Take(limit).ToList(), invoices.Count > limit);
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
    public async Task<ActionResult<IReadOnlyCollection<BillingPlan>>> GetPlansAsync(string id)
    {
        var organization = await GetModelAsync(id);
        if (organization is null)
            return NotFound();

        var plans = Request.IsGlobalAdmin()
            ? _plans.Plans.ToList()
            : _plans.Plans.Where(p => !p.IsHidden || p.Id == organization.PlanId).ToList();

        var currentPlan = new BillingPlan
        {
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

        int idx = plans.FindIndex(p => p.Id == organization.PlanId);
        if (idx >= 0)
            plans[idx] = currentPlan;
        else
            plans.Add(currentPlan);

        return Ok(plans);
    }

    /// <summary>
    /// Change plan
    /// </summary>
    /// <remarks>
    /// Upgrades or downgrades the organizations plan.
    /// Accepts parameters via JSON body (preferred) or query string (legacy).
    /// </remarks>
    /// <param name="id">The identifier of the organization.</param>
    /// <param name="model">The plan change request (JSON body).</param>
    /// <param name="planId">Legacy query parameter: the plan identifier.</param>
    /// <param name="stripeToken">Legacy query parameter: the Stripe token.</param>
    /// <param name="last4">Legacy query parameter: last four digits of the card.</param>
    /// <param name="couponId">Legacy query parameter: the coupon identifier.</param>
    /// <response code="404">The organization was not found.</response>
    [HttpPost]
    [Route("{id:objectid}/change-plan")]
    public async Task<ActionResult<ChangePlanResult>> ChangePlanAsync(
        string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ChangePlanRequest? model = null,
        [FromQuery] string? planId = null,
        [FromQuery] string? stripeToken = null,
        [FromQuery] string? last4 = null,
        [FromQuery] string? couponId = null)
    {
        // Support legacy clients that send query parameters instead of a JSON body
        model ??= new ChangePlanRequest { PlanId = planId ?? String.Empty };
        if (String.IsNullOrEmpty(model.PlanId) && !String.IsNullOrEmpty(planId))
            model.PlanId = planId;
        if (String.IsNullOrEmpty(model.StripeToken) && !String.IsNullOrEmpty(stripeToken))
            model.StripeToken = stripeToken;
        if (String.IsNullOrEmpty(model.Last4) && !String.IsNullOrEmpty(last4))
            model.Last4 = last4;
        if (String.IsNullOrEmpty(model.CouponId) && !String.IsNullOrEmpty(couponId))
            model.CouponId = couponId;

        if (String.IsNullOrEmpty(id) || !CanAccessOrganization(id))
            return NotFound();

        using var _ = _logger.BeginScope(new ExceptionlessState().Tag("Change Plan").Organization(id)
            .Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).SetHttpContext(HttpContext));

        if (!_options.StripeOptions.EnableBilling)
            return NotFound();

        var organization = await GetModelAsync(id, false);
        if (organization is null)
            return NotFound();

        var plan = _billingManager.GetBillingPlan(model.PlanId);
        if (plan is null)
        {
            ModelState.AddModelError(nameof(model.PlanId), "Invalid PlanId.");
            return ValidationProblem(ModelState);
        }

        if (String.Equals(organization.PlanId, plan.Id) && String.Equals(_plans.FreePlan.Id, plan.Id))
            return Ok(ChangePlanResult.SuccessWithMessage("Your plan was not changed as you were already on the free plan."));

        // Only see if they can downgrade a plan if the plans are different.
        if (!String.Equals(organization.PlanId, plan.Id))
        {
            var result = await _billingManager.CanDownGradeAsync(organization, plan, CurrentUser);
            if (!result.Success)
                return Ok(result);
        }

        var client = new StripeClient(_options.StripeOptions.StripeApiKey);
        var customerService = new CustomerService(client);
        var subscriptionService = new SubscriptionService(client);
        var paymentMethodService = new PaymentMethodService(client);

        // Detect if stripeToken is a legacy token (tok_) or modern PaymentMethod (pm_)
        bool isPaymentMethod = model.StripeToken?.StartsWith("pm_", StringComparison.Ordinal) == true;

        try
        {
            // If they are on a paid plan and then downgrade to a free plan then cancel their stripe subscription.
            if (!String.Equals(organization.PlanId, _plans.FreePlan.Id) && String.Equals(plan.Id, _plans.FreePlan.Id))
            {
                if (!String.IsNullOrEmpty(organization.StripeCustomerId))
                {
                    var subs = await subscriptionService.ListAsync(new SubscriptionListOptions { Customer = organization.StripeCustomerId });
                    foreach (var sub in subs.Where(s => !s.CanceledAt.HasValue))
                        await subscriptionService.CancelAsync(sub.Id, new SubscriptionCancelOptions());
                }

                organization.BillingStatus = BillingStatus.Trialing;
                organization.RemoveSuspension();
            }
            else if (String.IsNullOrEmpty(organization.StripeCustomerId))
            {
                if (String.IsNullOrEmpty(model.StripeToken))
                    return Ok(ChangePlanResult.FailWithMessage("Billing information was not set."));

                organization.SubscribeDate = _timeProvider.GetUtcNow().UtcDateTime;

                var createCustomer = new CustomerCreateOptions
                {
                    Description = organization.Name,
                    Email = CurrentUser.EmailAddress
                };

                if (isPaymentMethod)
                {
                    createCustomer.PaymentMethod = model.StripeToken;
                    createCustomer.InvoiceSettings = new CustomerInvoiceSettingsOptions
                    {
                        DefaultPaymentMethod = model.StripeToken
                    };
                }
                else
                {
                    createCustomer.Source = model.StripeToken;
                }

                var customer = await customerService.CreateAsync(createCustomer);

                // Create subscription separately (Plan on CustomerCreateOptions is deprecated)
                var subscriptionOptions = new SubscriptionCreateOptions
                {
                    Customer = customer.Id,
                    Items = [new SubscriptionItemOptions { Price = model.PlanId }]
                };

                if (isPaymentMethod)
                    subscriptionOptions.DefaultPaymentMethod = model.StripeToken;

                if (!String.IsNullOrWhiteSpace(model.CouponId))
                    subscriptionOptions.Discounts = [new SubscriptionDiscountOptions { Coupon = model.CouponId }];

                await subscriptionService.CreateAsync(subscriptionOptions);

                organization.BillingStatus = BillingStatus.Active;
                organization.RemoveSuspension();
                organization.StripeCustomerId = customer.Id;
                organization.CardLast4 = model.Last4;
            }
            else
            {
                var update = new SubscriptionUpdateOptions { Items = [] };
                var create = new SubscriptionCreateOptions { Customer = organization.StripeCustomerId, Items = [] };
                bool cardUpdated = false;

                var customerUpdateOptions = new CustomerUpdateOptions { Description = organization.Name };
                if (!Request.IsGlobalAdmin())
                    customerUpdateOptions.Email = CurrentUser.EmailAddress;

                // Start subscription list fetch immediately — it's independent of customer/payment ops
                var listSubscriptionsTask = subscriptionService.ListAsync(new SubscriptionListOptions { Customer = organization.StripeCustomerId });

                if (!String.IsNullOrEmpty(model.StripeToken))
                {
                    if (isPaymentMethod)
                    {
                        // Attach runs in parallel with listSubscriptionsTask
                        await paymentMethodService.AttachAsync(model.StripeToken, new PaymentMethodAttachOptions
                        {
                            Customer = organization.StripeCustomerId
                        });
                        customerUpdateOptions.InvoiceSettings = new CustomerInvoiceSettingsOptions
                        {
                            DefaultPaymentMethod = model.StripeToken
                        };
                    }
                    else
                    {
                        customerUpdateOptions.Source = model.StripeToken;
                    }
                    cardUpdated = true;
                }

                // Customer update and subscription list are independent — run in parallel
                await Task.WhenAll(
                    customerService.UpdateAsync(organization.StripeCustomerId, customerUpdateOptions),
                    listSubscriptionsTask
                );

                var subscriptionList = await listSubscriptionsTask;
                var subscription = subscriptionList.FirstOrDefault(s => !s.CanceledAt.HasValue);
                if (subscription is not null && subscription.Items.Data.Count > 0)
                {
                    update.Items.Add(new SubscriptionItemOptions { Id = subscription.Items.Data[0].Id, Price = model.PlanId });
                    if (!String.IsNullOrWhiteSpace(model.CouponId))
                        update.Discounts = [new SubscriptionDiscountOptions { Coupon = model.CouponId }];
                    await subscriptionService.UpdateAsync(subscription.Id, update);
                }
                else if (subscription is not null)
                {
                    _logger.LogWarning("Subscription {SubscriptionId} has no items for organization {OrganizationId}, adding new item", subscription.Id, id);
                    update.Items.Add(new SubscriptionItemOptions { Price = model.PlanId });
                    if (!String.IsNullOrWhiteSpace(model.CouponId))
                        update.Discounts = [new SubscriptionDiscountOptions { Coupon = model.CouponId }];
                    await subscriptionService.UpdateAsync(subscription.Id, update);
                }
                else
                {
                    create.Items.Add(new SubscriptionItemOptions { Price = model.PlanId });
                    if (!String.IsNullOrWhiteSpace(model.CouponId))
                        create.Discounts = [new SubscriptionDiscountOptions { Coupon = model.CouponId }];
                    await subscriptionService.CreateAsync(create);
                }

                if (cardUpdated)
                    organization.CardLast4 = model.Last4;

                organization.BillingStatus = BillingStatus.Active;
                organization.RemoveSuspension();
            }

            _billingManager.ApplyBillingPlan(organization, plan, CurrentUser);
            await _repository.SaveAsync(organization, o => o.Cache().Originals());
            await _messagePublisher.PublishAsync(new PlanChanged { OrganizationId = organization.Id });
        }
        catch (StripeException ex)
        {
            _logger.LogCritical(ex, "An error occurred while trying to update your billing plan: {Message}", ex.Message);
            return Ok(ChangePlanResult.FailWithMessage("An error occurred while changing plans. Please try again or contact support."));
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "An unexpected error occurred while trying to update your billing plan");
            return Ok(ChangePlanResult.FailWithMessage("An error occurred while changing plans. Please try again."));
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
    public async Task<ActionResult<User>> AddUserAsync(string id, string email)
    {
        if (String.IsNullOrEmpty(id) || !CanAccessOrganization(id) || String.IsNullOrEmpty(email))
            return NotFound();

        var organization = await GetModelAsync(id);
        if (organization is null)
            return NotFound();

        if (!await _billingManager.CanAddUserAsync(organization))
            return PlanLimitReached("Please upgrade your plan to add an additional user.");

        var user = await _userRepository.GetByEmailAddressAsync(email);
        if (user is not null)
        {
            if (!user.OrganizationIds.Contains(organization.Id))
            {
                user.OrganizationIds.Add(organization.Id);
                await _userRepository.SaveAsync(user, o => o.Cache());
                await _messagePublisher.PublishAsync(new UserMembershipChanged
                {
                    ChangeType = ChangeType.Added,
                    UserId = user.Id,
                    OrganizationId = organization.Id
                });
            }

            await _mailer.SendOrganizationAddedAsync(CurrentUser, organization, user);
        }
        else
        {
            var invite = organization.Invites.FirstOrDefault(i => String.Equals(i.EmailAddress, email, StringComparison.OrdinalIgnoreCase));
            if (invite is null)
            {
                invite = new Invite
                {
                    Token = StringExtensions.GetNewToken(),
                    EmailAddress = email.ToLowerInvariant(),
                    DateAdded = _timeProvider.GetUtcNow().UtcDateTime
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
    public async Task<IActionResult> RemoveUserAsync(string id, string email)
    {
        var organization = await GetModelAsync(id, false);
        if (organization is null)
            return NotFound();

        var user = await _userRepository.GetByEmailAddressAsync(email);
        if (user is null || !user.OrganizationIds.Contains(id))
        {
            var invite = organization.Invites.FirstOrDefault(i => String.Equals(i.EmailAddress, email, StringComparison.OrdinalIgnoreCase));
            if (invite is null)
                return Ok();

            organization.Invites.Remove(invite);
            await _repository.SaveAsync(organization, o => o.Cache());
        }
        else
        {
            if (!user.OrganizationIds.Contains(organization.Id))
                return BadRequest();

            var organizationUsers = await _userRepository.GetByOrganizationIdAsync(organization.Id);
            if (organizationUsers.Total is 1)
                return BadRequest("An organization must contain at least one user.");

            await _organizationService.CleanupProjectNotificationSettingsAsync(organization, [user.Id]);

            user.OrganizationIds.Remove(organization.Id);
            await _userRepository.SaveAsync(user, o => o.Cache());
            await _messagePublisher.PublishAsync(new UserMembershipChanged
            {
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
    public async Task<IActionResult> SuspendAsync(string id, SuspensionCode code, string? notes = null)
    {
        var organization = await GetModelAsync(id, false);
        if (organization is null)
            return NotFound();

        organization.IsSuspended = true;
        organization.SuspensionDate = _timeProvider.GetUtcNow().UtcDateTime;
        organization.SuspendedByUserId = CurrentUser.Id;
        organization.SuspensionCode = code;
        organization.SuspensionNotes = notes;
        await _repository.SaveAsync(organization, o => o.Cache().Originals());

        return Ok();
    }

    [HttpDelete]
    [Route("{id:objectid}/suspend")]
    [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> UnsuspendAsync(string id)
    {
        var organization = await GetModelAsync(id, false);
        if (organization is null)
            return NotFound();

        organization.IsSuspended = false;
        organization.SuspensionDate = null;
        organization.SuspendedByUserId = null;
        organization.SuspensionCode = null;
        organization.SuspensionNotes = null;
        await _repository.SaveAsync(organization, o => o.Cache().Originals());

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
    public async Task<IActionResult> PostDataAsync(string id, string key, ValueFromBody<string> value)
    {
        if (String.IsNullOrWhiteSpace(key) || String.IsNullOrWhiteSpace(value?.Value) || key.StartsWith('-'))
            return BadRequest();

        var organization = await GetModelAsync(id, false);
        if (organization is null)
            return NotFound();

        organization.Data ??= new DataDictionary();
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
    public async Task<IActionResult> DeleteDataAsync(string id, string key)
    {
        var organization = await GetModelAsync(id, false);
        if (organization is null)
            return NotFound();

        if (organization.Data is not null && organization.Data.Remove(key))
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
    public async Task<IActionResult> IsNameAvailableAsync(string name)
    {
        if (await IsOrganizationNameAvailableInternalAsync(name))
            return StatusCode(StatusCodes.Status204NoContent);

        return StatusCode(StatusCodes.Status201Created);
    }

    private async Task<bool> IsOrganizationNameAvailableInternalAsync(string name)
    {
        if (String.IsNullOrWhiteSpace(name))
            return false;

        string decodedName = Uri.UnescapeDataString(name).Trim().ToLowerInvariant();
        var results = await _repository.GetByIdsAsync(GetAssociatedOrganizationIds().ToArray(), o => o.Cache());
        return !results.Any(o => String.Equals(o.Name.Trim().ToLowerInvariant(), decodedName, StringComparison.OrdinalIgnoreCase));
    }

    protected override async Task<PermissionResult> CanAddAsync(Organization value)
    {
        if (String.IsNullOrEmpty(value.Name))
            return PermissionResult.DenyWithMessage("Organization name is required.");

        if (!await IsOrganizationNameAvailableInternalAsync(value.Name))
            return PermissionResult.DenyWithMessage("A organization with this name already exists.");

        if (!await _billingManager.CanAddOrganizationAsync(CurrentUser))
            return PermissionResult.DenyWithPlanLimitReached("Please upgrade your plan to add an additional organization.");

        return await base.CanAddAsync(value);
    }

    protected override async Task<Organization> AddModelAsync(Organization value)
    {
        var user = CurrentUser;
        var plan = !_options.StripeOptions.EnableBilling || user.Roles.Contains(AuthorizationRoles.GlobalAdmin)
            ? _plans.UnlimitedPlan
            : _plans.FreePlan;
        _billingManager.ApplyBillingPlan(value, plan, user);

        var organization = await base.AddModelAsync(value);

        user.OrganizationIds.Add(organization.Id);
        await _userRepository.SaveAsync(user, o => o.Cache());
        await _messagePublisher.PublishAsync(new UserMembershipChanged
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            ChangeType = ChangeType.Added
        });

        return organization;
    }

    protected override async Task<PermissionResult> CanUpdateAsync(Organization original, Delta<NewOrganization> changes)
    {
        var changed = changes.GetEntity();
        if (!await IsOrganizationNameAvailableInternalAsync(changed.Name))
            return PermissionResult.DenyWithMessage("A organization with this name already exists.");

        return await base.CanUpdateAsync(original, changes);
    }

    protected override async Task<PermissionResult> CanDeleteAsync(Organization value)
    {
        if (!String.IsNullOrEmpty(value.StripeCustomerId) && !User.IsInRole(AuthorizationRoles.GlobalAdmin))
            return PermissionResult.DenyWithMessage("An organization cannot be deleted if it has a subscription.", value.Id);

        var organizationProjects = await _projectRepository.GetByOrganizationIdAsync(value.Id);
        var projects = organizationProjects.Documents.ToList();
        if (!User.IsInRole(AuthorizationRoles.GlobalAdmin) && projects.Count > 0)
            return PermissionResult.DenyWithMessage("An organization cannot be deleted if it contains any projects.", value.Id);

        return await base.CanDeleteAsync(value);
    }

    protected override async Task AfterResultMapAsync<TDestination>(ICollection<TDestination> models)
    {
        await base.AfterResultMapAsync(models);

        var viewOrganizations = models.OfType<ViewOrganization>().ToList();
        foreach (var viewOrganization in viewOrganizations)
        {
            var realTimeUsage = await _usageService.GetUsageAsync(viewOrganization.Id);

            // ensure 12 months of usage
            viewOrganization.EnsureUsage(_timeProvider);
            viewOrganization.TrimUsage(_timeProvider);

            var currentUsage = viewOrganization.GetCurrentUsage(_timeProvider);
            currentUsage.Limit = realTimeUsage.CurrentUsage.Limit;
            currentUsage.Total = realTimeUsage.CurrentUsage.Total;
            currentUsage.Blocked = realTimeUsage.CurrentUsage.Blocked;
            currentUsage.Discarded = realTimeUsage.CurrentUsage.Discarded;
            currentUsage.TooBig = realTimeUsage.CurrentUsage.TooBig;

            var currentHourUsage = viewOrganization.GetCurrentHourlyUsage(_timeProvider);
            currentHourUsage.Total = realTimeUsage.CurrentHourUsage.Total;
            currentHourUsage.Blocked = realTimeUsage.CurrentHourUsage.Blocked;
            currentHourUsage.Discarded = realTimeUsage.CurrentHourUsage.Discarded;
            currentHourUsage.TooBig = realTimeUsage.CurrentHourUsage.TooBig;

            viewOrganization.IsThrottled = realTimeUsage.IsThrottled;
            viewOrganization.IsOverRequestLimit = await OrganizationExtensions.IsOverRequestLimitAsync(viewOrganization.Id, _cacheClient, _options.ApiThrottleLimit, _timeProvider);
        }
    }

    private async Task<ViewOrganization> PopulateOrganizationStatsAsync(ViewOrganization organization)
    {
        return (await PopulateOrganizationStatsAsync([organization])).Single();
    }

    private async Task<List<ViewOrganization>> PopulateOrganizationStatsAsync(List<ViewOrganization> viewOrganizations)
    {
        if (viewOrganizations.Count <= 0)
            return viewOrganizations;

        int maximumRetentionDays = _options.MaximumRetentionDays;
        var organizations = viewOrganizations.Select(o => new Organization { Id = o.Id, CreatedUtc = o.CreatedUtc, RetentionDays = o.RetentionDays }).ToList();
        var sf = new AppFilter(organizations);
        var systemFilter = new RepositoryQuery<PersistentEvent>().AppFilter(sf).DateRange(organizations.GetRetentionUtcCutoff(maximumRetentionDays, _timeProvider), _timeProvider.GetUtcNow().UtcDateTime, (PersistentEvent e) => e.Date).Index(organizations.GetRetentionUtcCutoff(maximumRetentionDays, _timeProvider), _timeProvider.GetUtcNow().UtcDateTime);
        var result = await _eventRepository.CountAsync(q => q
            .SystemFilter(systemFilter)
            .AggregationsExpression($"terms:(organization_id~{viewOrganizations.Count} cardinality:stack_id)")
            .EnforceEventStackFilter(false));

        foreach (var organization in viewOrganizations)
        {
            var organizationStats = result.Aggregations.Terms<string>("terms_organization_id")?.Buckets.FirstOrDefault(t => t.Key == organization.Id);
            organization.EventCount = organizationStats?.Total ?? 0;
            organization.StackCount = (long?)organizationStats?.Aggregations.Cardinality("cardinality_stack_id")?.Value ?? 0;
            organization.ProjectCount = await _projectRepository.GetCountByOrganizationIdAsync(organization.Id);
        }

        return viewOrganizations;
    }
}
