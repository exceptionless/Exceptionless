using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Services;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Mapping;
using Exceptionless.Web.Models;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Exceptionless.Web.Utility;
using Stripe;
using Foundatio.Mediator;
using PermissionResult = Exceptionless.Web.Controllers.PermissionResult;
using DataDictionary = Exceptionless.Core.Models.DataDictionary;
using Invoice = Exceptionless.Web.Models.Invoice;
using InvoiceLineItem = Exceptionless.Web.Models.InvoiceLineItem;

namespace Exceptionless.Web.Api.Handlers;

public class OrganizationHandler(
    OrganizationService organizationService,
    IOrganizationRepository repository,
    ICacheClient cacheClient,
    IEventRepository eventRepository,
    IUserRepository userRepository,
    IProjectRepository projectRepository,
    BillingManager billingManager,
    BillingPlans plans,
    UsageService usageService,
    IStripeBillingClient stripeBillingClient,
    IMailer mailer,
    IMessagePublisher messagePublisher,
    ApiMapper mapper,
    AppOptions options,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<OrganizationHandler>();
    private HttpContext HttpContext => httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is unavailable.");

    public async Task<Result<IReadOnlyCollection<ViewOrganization>>> Handle(GetOrganizations message)
    {
        var organizations = await GetModelsAsync(message.Context.Request.GetAssociatedOrganizationIds().ToArray());
        if (organizations.Count == 0)
            return Result<IReadOnlyCollection<ViewOrganization>>.Success(Array.Empty<ViewOrganization>());

        var sf = new AppFilter(organizations) { IsUserOrganizationsFilter = true };
        organizations = String.IsNullOrWhiteSpace(message.Filter)
            ? organizations
            : (await repository.GetByFilterAsync(sf, message.Filter, null, o => o.PageLimit(Pagination.MaximumSkip))).Documents;
        var viewOrganizations = mapper.MapToViewOrganizations(organizations);
        await AfterResultMapAsync(viewOrganizations);

        if (IsStatsMode(message.Mode))
            return Result<IReadOnlyCollection<ViewOrganization>>.Success(await PopulateOrganizationStatsAsync(viewOrganizations));

        return Result<IReadOnlyCollection<ViewOrganization>>.Success(viewOrganizations);
    }

    public async Task<Result<PagedResult<ViewOrganization>>> Handle(GetAdminOrganizations message)
    {
        int page = Pagination.GetPage(message.Page);
        int limit = Pagination.GetLimit(message.Limit);
        var organizations = await repository.GetByCriteriaAsync(message.Criteria, o => o.PageNumber(page).PageLimit(limit), message.Sort, message.Paid, message.Suspended);
        var viewOrganizations = mapper.MapToViewOrganizations(organizations.Documents);
        await AfterResultMapAsync(viewOrganizations);

        if (IsStatsMode(message.Mode))
            return new PagedResult<ViewOrganization>(await PopulateOrganizationStatsAsync(viewOrganizations), organizations.HasMore, page, organizations.Total);

        return new PagedResult<ViewOrganization>(viewOrganizations, organizations.HasMore, page, organizations.Total);
    }

    public async Task<Result<BillingPlanStats>> Handle(GetOrganizationPlanStats message)
    {
        return await repository.GetBillingPlanStatsAsync();
    }

    public async Task<Result<ViewOrganization>> Handle(GetOrganizationById message)
    {
        var organization = await GetModelAsync(message.Id);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        var viewOrganization = mapper.MapToViewOrganization(organization);
        await AfterResultMapAsync([viewOrganization]);

        if (IsStatsMode(message.Mode))
            return await PopulateOrganizationStatsAsync(viewOrganization);

        return viewOrganization;
    }

    public async Task<Result<ViewOrganization>> Handle(CreateOrganization message)
    {
        if (message.Organization is null)
            return Result.BadRequest("Organization value is required.");

        var model = mapper.MapToOrganization(message.Organization);
        var error = await CanAddAsync(model, message.Context);
        if (error is not null)
            return error;

        model = await AddModelAsync(model, message.Context);
        var viewModel = mapper.MapToViewOrganization(model);
        await AfterResultMapAsync([viewModel]);
        return Result<ViewOrganization>.Created(viewModel, $"/api/v2/organizations/{model.Id}");
    }

    public async Task<Result<ViewOrganization>> Handle(UpdateOrganizationMessage message)
    {
        var original = await GetModelAsync(message.Id, useCache: false);
        if (original is null)
            return Result.NotFound("Organization not found.");

        if (!message.Changes.GetChangedPropertyNames().Any())
            return await MapToViewAsync(original);

        var error = await CanUpdateAsync(original, message.Changes, message.Context);
        if (error is not null)
            return error;

        message.Changes.Patch(original);
        await repository.SaveAsync(original, o => o.Cache());
        return await MapToViewAsync(original);
    }

    public async Task<Result<ModelActionResults>> Handle(DeleteOrganizations message)
    {
        var items = await GetModelsAsync(message.Ids, useCache: false);
        if (items.Count == 0)
            return Result.NotFound("Organization not found.");

        var results = new ModelActionResults();
        results.AddNotFound(message.Ids.Except(items.Select(i => i.Id)));

        var deletableItems = items.ToList();
        foreach (var model in items)
        {
            var permission = await CanDeleteAsync(model, message.Context);
            if (permission.Allowed)
                continue;

            deletableItems.Remove(model);
            results.Failure.Add(permission);
        }

        if (deletableItems.Count == 0)
            return results.Failure.Count == 1 ? Result<ModelActionResults>.FromResult(PermissionToResult(results.Failure.First())) : results;

        IEnumerable<string> workIds = await DeleteModelsAsync(deletableItems, message.Context);
        if (results.Failure.Count == 0)
            return new ModelActionResults { Workers = workIds.ToList() };

        results.Workers.AddRange(workIds);
        results.Success.AddRange(deletableItems.Select(i => i.Id));
        return results;
    }

    public async Task<Result<Invoice>> Handle(GetInvoice message)
    {
        if (!options.StripeOptions.EnableBilling)
            return Result.NotFound("Organization not found.");

        using var _ = _logger.BeginScope(new ExceptionlessState().Tag("Invoice").Identity(GetCurrentUser(message.Context).EmailAddress)
            .Property("User", GetCurrentUser(message.Context)).SetHttpContext(message.Context));

        string invoiceId = message.Id;
        if (!invoiceId.StartsWith("in_", StringComparison.Ordinal))
            invoiceId = "in_" + invoiceId;

        Stripe.Invoice? stripeInvoice = null;
        try
        {
            stripeInvoice = await stripeBillingClient.GetInvoiceAsync(invoiceId);
        }
        catch (StripeException ex)
        {
            _logger.LogCritical(ex, "Error getting invoice ({InvoiceId}): {Message}", invoiceId, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Unexpected error getting invoice ({InvoiceId}): {Message}", invoiceId, ex.Message);
        }

        if (String.IsNullOrEmpty(stripeInvoice?.CustomerId))
            return Result.NotFound("Organization not found.");

        var organization = await repository.GetByStripeCustomerIdAsync(stripeInvoice.CustomerId);
        if (organization is null || !message.Context.Request.CanAccessOrganization(organization.Id))
            return Result.NotFound("Organization not found.");

        var invoice = new Invoice
        {
            Id = stripeInvoice.Id.Substring(3),
            OrganizationId = organization.Id,
            OrganizationName = organization.Name,
            Date = stripeInvoice.Created,
            Paid = String.Equals(stripeInvoice.Status, "paid", StringComparison.OrdinalIgnoreCase),
            Total = stripeInvoice.Total / 100.0m
        };

        foreach (var line in stripeInvoice.Lines.Data)
        {
            var item = new InvoiceLineItem { Amount = line.Amount / 100.0m, Description = line.Description };

            var priceId = line.Pricing?.PriceDetails?.PriceId;
            if (!String.IsNullOrEmpty(priceId))
            {
                var billingPlan = billingManager.GetBillingPlan(priceId);
                if (billingPlan is null)
                    _logger.LogWarning("Billing plan not found for price {PriceId} on invoice {InvoiceId}", priceId, invoiceId);

                string planName = billingPlan?.Name ?? priceId;
                string interval = priceId.EndsWith("_YEARLY", StringComparison.OrdinalIgnoreCase) ? "year" : "month";
                item.Description = $"Exceptionless - {planName} Plan ({line.Amount / 100.0m:c}/{interval})";
            }

            var periodStart = line.Period.Start >= DateTime.MinValue ? line.Period.Start : stripeInvoice.PeriodStart;
            var periodEnd = line.Period.End >= DateTime.MinValue ? line.Period.End : stripeInvoice.PeriodEnd;
            item.Date = $"{periodStart.ToShortDateString()} - {periodEnd.ToShortDateString()}";
            invoice.Items.Add(item);
        }

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

        return invoice;
    }

    public async Task<Result<PagedResult<InvoiceGridModel>>> Handle(GetInvoices message)
    {
        if (!options.StripeOptions.EnableBilling)
            return Result.NotFound("Organization not found.");

        var organization = await GetModelAsync(message.Id);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (String.IsNullOrWhiteSpace(organization.StripeCustomerId))
            return new PagedResult<InvoiceGridModel>(new List<InvoiceGridModel>(), false);

        string? before = message.Before;
        string? after = message.After;
        if (!String.IsNullOrEmpty(before) && !before.StartsWith("in_", StringComparison.Ordinal))
            before = "in_" + before;
        if (!String.IsNullOrEmpty(after) && !after.StartsWith("in_", StringComparison.Ordinal))
            after = "in_" + after;

        var invoiceOptions = new InvoiceListOptions { Customer = organization.StripeCustomerId, Limit = message.Limit + 1, EndingBefore = before, StartingAfter = after };
        var invoices = mapper.MapToInvoiceGridModels(await stripeBillingClient.ListInvoicesAsync(invoiceOptions));
        return new PagedResult<InvoiceGridModel>(invoices.Take(message.Limit).ToList(), invoices.Count > message.Limit);
    }

    public async Task<Result<IReadOnlyCollection<BillingPlan>>> Handle(GetPlans message)
    {
        var organization = await GetModelAsync(message.Id);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        var availablePlans = message.Context.Request.IsGlobalAdmin()
            ? plans.Plans.ToList()
            : plans.Plans.Where(p => !p.IsHidden || String.Equals(p.Id, organization.PlanId, StringComparison.OrdinalIgnoreCase)).ToList();

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

        int idx = availablePlans.FindIndex(p => String.Equals(p.Id, organization.PlanId, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
            availablePlans[idx] = currentPlan;
        else
            availablePlans.Add(currentPlan);

        return Result<IReadOnlyCollection<BillingPlan>>.Success(availablePlans);
    }

    public async Task<Result<ChangePlanResult>> Handle(ChangeOrganizationPlan message)
    {
        var model = message.Model ?? new ChangePlanRequest { PlanId = message.PlanId ?? String.Empty };
        if (String.IsNullOrEmpty(model.PlanId) && !String.IsNullOrEmpty(message.PlanId))
            model.PlanId = message.PlanId;
        if (String.IsNullOrEmpty(model.StripeToken) && !String.IsNullOrEmpty(message.StripeToken))
            model.StripeToken = message.StripeToken;
        if (String.IsNullOrEmpty(model.Last4) && !String.IsNullOrEmpty(message.Last4))
            model.Last4 = message.Last4;
        if (String.IsNullOrEmpty(model.CouponId) && !String.IsNullOrEmpty(message.CouponId))
            model.CouponId = message.CouponId;

        if (String.IsNullOrEmpty(message.Id) || !message.Context.Request.CanAccessOrganization(message.Id))
            return Result.NotFound("Organization not found.");

        using var _ = _logger.BeginScope(new ExceptionlessState().Tag("Change Plan").Organization(message.Id)
            .Identity(GetCurrentUser(message.Context).EmailAddress).Property("User", GetCurrentUser(message.Context)).SetHttpContext(message.Context));

        if (!options.StripeOptions.EnableBilling)
            return Result.NotFound("Organization not found.");

        var organization = await GetModelAsync(message.Id, useCache: false);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        var plan = billingManager.GetBillingPlan(model.PlanId);
        if (plan is null)
        {
            _logger.LogWarning("Plan {PlanId} not found for organization {OrganizationId}", model.PlanId, message.Id);
            return Result.Invalid(ValidationError.Create("general", "Invalid plan. Please select a valid plan."));
        }

        if (String.Equals(organization.PlanId, plan.Id) && String.Equals(plans.FreePlan.Id, plan.Id))
            return ChangePlanResult.SuccessWithMessage("Your plan was not changed as you were already on the free plan.");

        if (!String.Equals(organization.PlanId, plan.Id))
        {
            var result = await billingManager.CanDownGradeAsync(organization, plan, GetCurrentUser(message.Context));
            if (!result.Success)
                return result;
        }

        bool isPaymentMethod = model.StripeToken?.StartsWith("pm_", StringComparison.Ordinal) is true;

        try
        {
            if (!String.Equals(organization.PlanId, plans.FreePlan.Id) && String.Equals(plan.Id, plans.FreePlan.Id))
            {
                if (!String.IsNullOrEmpty(organization.StripeCustomerId))
                {
                    var subs = await stripeBillingClient.ListSubscriptionsAsync(new SubscriptionListOptions { Customer = organization.StripeCustomerId });
                    foreach (var sub in subs.Where(s => !s.CanceledAt.HasValue))
                        await stripeBillingClient.CancelSubscriptionAsync(sub.Id, new SubscriptionCancelOptions());
                }

                organization.BillingStatus = BillingStatus.Trialing;
                organization.RemoveSuspension();
            }
            else if (String.IsNullOrEmpty(organization.StripeCustomerId))
            {
                if (String.IsNullOrEmpty(model.StripeToken))
                    return ChangePlanResult.FailWithMessage("Billing information was not set.");

                organization.SubscribeDate = timeProvider.GetUtcNow().UtcDateTime;

                var createCustomer = new CustomerCreateOptions
                {
                    Description = organization.Name,
                    Email = GetCurrentUser(message.Context).EmailAddress
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

                var customer = await stripeBillingClient.CreateCustomerAsync(createCustomer);
                organization.StripeCustomerId = customer.Id;
                organization.CardLast4 = model.Last4;
                await repository.SaveAsync(organization, o => o.Cache());

                var subscriptionOptions = new SubscriptionCreateOptions
                {
                    Customer = customer.Id,
                    Items = [new SubscriptionItemOptions { Price = model.PlanId }]
                };

                if (isPaymentMethod)
                    subscriptionOptions.DefaultPaymentMethod = model.StripeToken;

                if (!String.IsNullOrWhiteSpace(model.CouponId))
                    subscriptionOptions.Discounts = [new SubscriptionDiscountOptions { Coupon = model.CouponId }];

                await stripeBillingClient.CreateSubscriptionAsync(subscriptionOptions);

                organization.BillingStatus = BillingStatus.Active;
                organization.RemoveSuspension();
            }
            else
            {
                var update = new SubscriptionUpdateOptions { Items = [] };
                var create = new SubscriptionCreateOptions { Customer = organization.StripeCustomerId, Items = [] };
                bool cardUpdated = false;

                var customerUpdateOptions = new CustomerUpdateOptions { Description = organization.Name };
                if (!message.Context.Request.IsGlobalAdmin())
                    customerUpdateOptions.Email = GetCurrentUser(message.Context).EmailAddress;

                var listSubscriptionsTask = stripeBillingClient.ListSubscriptionsAsync(new SubscriptionListOptions { Customer = organization.StripeCustomerId });

                if (!String.IsNullOrEmpty(model.StripeToken))
                {
                    if (isPaymentMethod)
                    {
                        await stripeBillingClient.AttachPaymentMethodAsync(model.StripeToken, new PaymentMethodAttachOptions
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

                await Task.WhenAll(
                    stripeBillingClient.UpdateCustomerAsync(organization.StripeCustomerId, customerUpdateOptions),
                    listSubscriptionsTask
                );

                var subscriptionList = await listSubscriptionsTask;
                var subscription = subscriptionList.FirstOrDefault(s => !s.CanceledAt.HasValue);
                if (subscription is not null && subscription.Items.Data.Count > 0)
                {
                    update.Items.Add(new SubscriptionItemOptions { Id = subscription.Items.Data[0].Id, Price = model.PlanId });
                    if (!String.IsNullOrWhiteSpace(model.CouponId))
                        update.Discounts = [new SubscriptionDiscountOptions { Coupon = model.CouponId }];
                    await stripeBillingClient.UpdateSubscriptionAsync(subscription.Id, update);
                }
                else if (subscription is not null)
                {
                    _logger.LogWarning("Subscription {SubscriptionId} has no items for organization {OrganizationId}, adding new item", subscription.Id, message.Id);
                    update.Items.Add(new SubscriptionItemOptions { Price = model.PlanId });
                    if (!String.IsNullOrWhiteSpace(model.CouponId))
                        update.Discounts = [new SubscriptionDiscountOptions { Coupon = model.CouponId }];
                    await stripeBillingClient.UpdateSubscriptionAsync(subscription.Id, update);
                }
                else
                {
                    create.Items.Add(new SubscriptionItemOptions { Price = model.PlanId });
                    if (!String.IsNullOrWhiteSpace(model.CouponId))
                        create.Discounts = [new SubscriptionDiscountOptions { Coupon = model.CouponId }];
                    await stripeBillingClient.CreateSubscriptionAsync(create);
                }

                if (cardUpdated)
                    organization.CardLast4 = model.Last4;

                if (organization.SubscribeDate is null || organization.SubscribeDate == DateTime.MinValue)
                    organization.SubscribeDate = timeProvider.GetUtcNow().UtcDateTime;

                organization.BillingStatus = BillingStatus.Active;
                organization.RemoveSuspension();
            }

            billingManager.ApplyBillingPlan(organization, plan, GetCurrentUser(message.Context));
            await repository.SaveAsync(organization, o => o.Cache().Originals());
            await messagePublisher.PublishAsync(new PlanChanged { OrganizationId = organization.Id });
        }
        catch (StripeException ex)
        {
            _logger.LogCritical(ex, "Error occurred update billing plan: {Message}", ex.Message);
            return ChangePlanResult.FailWithMessage("An error occurred while changing plans. Please try again or contact support.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "An unexpected error occurred while trying to update your billing plan: {Message}", ex.Message);
            return ChangePlanResult.FailWithMessage("An error occurred while changing plans. Please try again.");
        }

        return new ChangePlanResult { Success = true };
    }

    public async Task<Result<User>> Handle(AddOrganizationUser message)
    {
        if (String.IsNullOrEmpty(message.Id) || !message.Context.Request.CanAccessOrganization(message.Id) || String.IsNullOrEmpty(message.Email))
            return Result.NotFound("Organization not found.");

        var organization = await GetModelAsync(message.Id);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (!await billingManager.CanAddUserAsync(organization))
            return Result.Invalid(ValidationError.Create("plan_limit", "Please upgrade your plan to add an additional user."));

        var user = await userRepository.GetByEmailAddressAsync(message.Email);
        if (user is not null)
        {
            if (!user.OrganizationIds.Contains(organization.Id))
            {
                user.OrganizationIds.Add(organization.Id);
                await userRepository.SaveAsync(user, o => o.Cache());
                await messagePublisher.PublishAsync(new UserMembershipChanged
                {
                    ChangeType = ChangeType.Added,
                    UserId = user.Id,
                    OrganizationId = organization.Id
                });
            }

            await mailer.SendOrganizationAddedAsync(GetCurrentUser(message.Context), organization, user);
        }
        else
        {
            var invite = organization.Invites.FirstOrDefault(i => String.Equals(i.EmailAddress, message.Email, StringComparison.OrdinalIgnoreCase));
            if (invite is null)
            {
                invite = new Invite
                {
                    Token = StringExtensions.GetNewToken(),
                    EmailAddress = message.Email.ToLowerInvariant(),
                    DateAdded = timeProvider.GetUtcNow().UtcDateTime
                };
                organization.Invites.Add(invite);
                await repository.SaveAsync(organization, o => o.Cache());
            }

            await mailer.SendOrganizationInviteAsync(GetCurrentUser(message.Context), organization, invite);
        }

        return new User { EmailAddress = message.Email };
    }

    public async Task<Result> Handle(RemoveOrganizationUser message)
    {
        var organization = await GetModelAsync(message.Id, useCache: false);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        var user = await userRepository.GetByEmailAddressAsync(message.Email);
        if (user is null || !user.OrganizationIds.Contains(message.Id))
        {
            var invite = organization.Invites.FirstOrDefault(i => String.Equals(i.EmailAddress, message.Email, StringComparison.OrdinalIgnoreCase));
            if (invite is null)
                return Result.Success();

            organization.Invites.Remove(invite);
            await repository.SaveAsync(organization, o => o.Cache());
        }
        else
        {
            if (!user.OrganizationIds.Contains(organization.Id))
                return Result.BadRequest("Invalid organization user.");

            var organizationUsers = await userRepository.GetByOrganizationIdAsync(organization.Id);
            if (organizationUsers.Total is 1)
                return Result.BadRequest("An organization must contain at least one user.");

            await organizationService.CleanupProjectNotificationSettingsAsync(organization, [user.Id]);
            await organizationService.RemoveUserSavedViewsAsync(organization.Id, user.Id);

            user.OrganizationIds.Remove(organization.Id);
            await userRepository.SaveAsync(user, o => o.Cache());
            await messagePublisher.PublishAsync(new UserMembershipChanged
            {
                ChangeType = ChangeType.Removed,
                UserId = user.Id,
                OrganizationId = organization.Id
            });
        }

        return Result.Success();
    }

    public async Task<Result> Handle(SuspendOrganization message)
    {
        var organization = await GetModelAsync(message.Id, useCache: false);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        organization.IsSuspended = true;
        organization.SuspensionDate = timeProvider.GetUtcNow().UtcDateTime;
        organization.SuspendedByUserId = GetCurrentUser(message.Context).Id;
        organization.SuspensionCode = message.Code;
        organization.SuspensionNotes = message.Notes;
        await repository.SaveAsync(organization, o => o.Cache().Originals());

        return Result.Success();
    }

    public async Task<Result> Handle(UnsuspendOrganization message)
    {
        var organization = await GetModelAsync(message.Id, useCache: false);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        organization.IsSuspended = false;
        organization.SuspensionDate = null;
        organization.SuspendedByUserId = null;
        organization.SuspensionCode = null;
        organization.SuspensionNotes = null;
        await repository.SaveAsync(organization, o => o.Cache().Originals());

        return Result.Success();
    }

    public async Task<Result> Handle(SetOrganizationData message)
    {
        if (String.IsNullOrWhiteSpace(message.Key) || String.IsNullOrWhiteSpace(message.Value?.Value) || message.Key.StartsWith('-'))
            return Result.BadRequest("Invalid key or value.");

        var organization = await GetModelAsync(message.Id, useCache: false);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        organization.Data ??= new DataDictionary();
        organization.Data[message.Key.Trim()] = message.Value.Value.Trim();
        await repository.SaveAsync(organization, o => o.Cache());

        return Result.Success();
    }

    public async Task<Result> Handle(DeleteOrganizationData message)
    {
        var organization = await GetModelAsync(message.Id, useCache: false);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (organization.Data is not null && organization.Data.Remove(message.Key))
            await repository.SaveAsync(organization, o => o.Cache());

        return Result.Success();
    }

    public async Task<Result> Handle(SetOrganizationFeature message)
    {
        var organization = await GetModelAsync(message.Id, useCache: false);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        var normalizedFeature = message.Feature.Trim().ToLowerInvariant();
        if (String.IsNullOrEmpty(normalizedFeature))
            return Result.BadRequest("Invalid feature flag.");

        organization.Features.Add(normalizedFeature);
        await repository.SaveAsync(organization, o => o.Cache());
        return Result.Success();
    }

    public async Task<Result> Handle(RemoveOrganizationFeature message)
    {
        var organization = await GetModelAsync(message.Id, useCache: false);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        var normalizedFeature = message.Feature.Trim().ToLowerInvariant();
        if (String.IsNullOrEmpty(normalizedFeature))
            return Result.BadRequest("Invalid feature flag.");

        if (organization.Features.Remove(normalizedFeature))
            await repository.SaveAsync(organization, o => o.Cache());

        return Result.Success();
    }

    public async Task<Result> Handle(CheckOrganizationName message)
    {
        if (await IsOrganizationNameAvailableInternalAsync(message.Name, message.Context))
            return Result.NoContent();

        return Result.Created();
    }

    private async Task<ViewOrganization> MapToViewAsync(Organization model)
    {
        var viewModel = mapper.MapToViewOrganization(model);
        await AfterResultMapAsync([viewModel]);
        return viewModel;
    }

    private async Task<Result<ViewOrganization>?> CanAddAsync(Organization value, HttpContext httpContext)
    {
        if (String.IsNullOrEmpty(value.Name))
            return Result.BadRequest("Organization name is required.");

        if (!await IsOrganizationNameAvailableInternalAsync(value.Name, httpContext))
            return Result.BadRequest("A organization with this name already exists.");

        if (!await billingManager.CanAddOrganizationAsync(GetCurrentUser(httpContext)))
            return Result.Invalid(ValidationError.Create("plan_limit", "Please upgrade your plan to add an additional organization."));

        return null;
    }

    private async Task<Organization> AddModelAsync(Organization value, HttpContext httpContext)
    {
        var user = GetCurrentUser(httpContext);
        var plan = !options.StripeOptions.EnableBilling || user.Roles.Contains(AuthorizationRoles.GlobalAdmin)
            ? plans.UnlimitedPlan
            : plans.FreePlan;
        billingManager.ApplyBillingPlan(value, plan, user);

        var organization = await repository.AddAsync(value, o => o.Cache());

        user.OrganizationIds.Add(organization.Id);
        await userRepository.SaveAsync(user, o => o.Cache());
        await messagePublisher.PublishAsync(new UserMembershipChanged
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            ChangeType = ChangeType.Added
        });

        return organization;
    }

    private async Task<Result<ViewOrganization>?> CanUpdateAsync(Organization original, Delta<NewOrganization> changes, HttpContext httpContext)
    {
        var changed = changes.GetEntity();
        if (!await IsOrganizationNameAvailableInternalAsync(changed.Name, httpContext))
            return Result.BadRequest("A organization with this name already exists.");

        if (changes.GetChangedPropertyNames().Contains("OrganizationId"))
            return Result.BadRequest("OrganizationId cannot be modified.");

        return null;
    }

    private async Task<PermissionResult> CanDeleteAsync(Organization value, HttpContext httpContext)
    {
        if (!String.IsNullOrEmpty(value.StripeCustomerId) && !messageIsGlobalAdmin(httpContext))
            return PermissionResult.DenyWithMessage("An organization cannot be deleted if it has a subscription.", value.Id);

        var organizationProjects = await projectRepository.GetByOrganizationIdAsync(value.Id);
        var projects = organizationProjects.Documents.ToList();
        if (!messageIsGlobalAdmin(httpContext) && projects.Count > 0)
            return PermissionResult.DenyWithMessage("An organization cannot be deleted if it contains any projects.", value.Id);

        return PermissionResult.Allow;
    }

    private async Task<IEnumerable<string>> DeleteModelsAsync(ICollection<Organization> organizations, HttpContext httpContext)
    {
        var user = GetCurrentUser(httpContext);
        foreach (var organization in organizations)
        {
            using var _ = _logger.BeginScope(new ExceptionlessState().Organization(organization.Id).Tag("Delete").Identity(user.EmailAddress).Property("User", user).SetHttpContext(httpContext));
            _logger.UserDeletingOrganization(user.Id, organization.Name, organization.Id);
            await organizationService.SoftDeleteOrganizationAsync(organization, user.Id);
        }

        return [];
    }

    private async Task<Organization?> GetModelAsync(string id, bool useCache = true)
    {
        if (String.IsNullOrEmpty(id))
            return null;

        var model = await repository.GetByIdAsync(id, o => o.Cache(useCache));
        if (model is null)
            return null;

        if (!HttpContext.Request.CanAccessOrganization(model.Id))
            return null;

        return model;
    }

    private async Task<IReadOnlyCollection<Organization>> GetModelsAsync(string[] ids, bool useCache = true)
    {
        if (ids.Length == 0)
            return [];

        var models = await repository.GetByIdsAsync(ids, o => o.Cache(useCache));
        return models.Where(m => HttpContext.Request.CanAccessOrganization(m.Id)).ToList();
    }

    private async Task AfterResultMapAsync<TDestination>(ICollection<TDestination> models)
    {
        foreach (var model in models.OfType<IData>())
            model.Data?.RemoveSensitiveData();

        var viewOrganizations = models.OfType<ViewOrganization>().ToList();
        foreach (var viewOrganization in viewOrganizations)
        {
            var realTimeUsage = await usageService.GetUsageAsync(viewOrganization.Id);
            viewOrganization.EnsureUsage(timeProvider);
            viewOrganization.TrimUsage(timeProvider);

            var currentUsage = viewOrganization.GetCurrentUsage(timeProvider);
            currentUsage.Limit = realTimeUsage.CurrentUsage.Limit;
            currentUsage.Total = realTimeUsage.CurrentUsage.Total;
            currentUsage.Blocked = realTimeUsage.CurrentUsage.Blocked;
            currentUsage.Discarded = realTimeUsage.CurrentUsage.Discarded;
            currentUsage.TooBig = realTimeUsage.CurrentUsage.TooBig;
            currentUsage.Deleted = realTimeUsage.CurrentUsage.Deleted;

            var currentHourUsage = viewOrganization.GetCurrentHourlyUsage(timeProvider);
            currentHourUsage.Total = realTimeUsage.CurrentHourUsage.Total;
            currentHourUsage.Blocked = realTimeUsage.CurrentHourUsage.Blocked;
            currentHourUsage.Discarded = realTimeUsage.CurrentHourUsage.Discarded;
            currentHourUsage.TooBig = realTimeUsage.CurrentHourUsage.TooBig;
            currentHourUsage.Deleted = realTimeUsage.CurrentHourUsage.Deleted;

            viewOrganization.IsThrottled = realTimeUsage.IsThrottled;
            viewOrganization.IsOverRequestLimit = await OrganizationExtensions.IsOverRequestLimitAsync(viewOrganization.Id, cacheClient, options.ApiThrottleLimit, timeProvider);
        }
    }

    private async Task<ViewOrganization> PopulateOrganizationStatsAsync(ViewOrganization organization)
    {
        return (await PopulateOrganizationStatsAsync([organization])).Single();
    }

    private async Task<List<ViewOrganization>> PopulateOrganizationStatsAsync(List<ViewOrganization> viewOrganizations)
    {
        if (viewOrganizations.Count == 0)
            return viewOrganizations;

        int maximumRetentionDays = options.MaximumRetentionDays;
        var organizations = viewOrganizations.Select(o => new Organization { Id = o.Id, CreatedUtc = o.CreatedUtc, RetentionDays = o.RetentionDays }).ToList();
        var sf = new AppFilter(organizations);
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var retentionUtcCutoff = organizations.GetRetentionUtcCutoff(maximumRetentionDays, timeProvider);
        var systemFilter = new RepositoryQuery<PersistentEvent>()
            .AppFilter(sf)
            .DateRange(retentionUtcCutoff, utcNow, (PersistentEvent e) => e.Date)
            .Index(retentionUtcCutoff, utcNow);
        var result = await eventRepository.CountAsync(q => q
            .SystemFilter(systemFilter)
            .AggregationsExpression($"terms:(organization_id~{viewOrganizations.Count} cardinality:stack_id)")
            .EnforceEventStackFilter(false));

        foreach (var organization in viewOrganizations)
        {
            var organizationStats = result.Aggregations.Terms<string>("terms_organization_id")?.Buckets.FirstOrDefault(t => t.Key == organization.Id);
            organization.EventCount = organizationStats?.Total ?? 0;
            organization.StackCount = (long?)organizationStats?.Aggregations.Cardinality("cardinality_stack_id")?.Value ?? 0;
            organization.ProjectCount = await projectRepository.GetCountByOrganizationIdAsync(organization.Id);
        }

        return viewOrganizations;
    }

    private async Task<bool> IsOrganizationNameAvailableInternalAsync(string name, HttpContext httpContext)
    {
        if (String.IsNullOrWhiteSpace(name))
            return false;

        string decodedName = Uri.UnescapeDataString(name).Trim().ToLowerInvariant();
        var results = await repository.GetByIdsAsync(httpContext.Request.GetAssociatedOrganizationIds().ToArray(), o => o.Cache());
        return !results.Any(o => String.Equals(o.Name.Trim().ToLowerInvariant(), decodedName, StringComparison.OrdinalIgnoreCase));
    }

    private static Result PermissionToResult(PermissionResult permission)
    {
        if (permission.StatusCode == StatusCodes.Status404NotFound)
            return Result.NotFound(permission.Message ?? "Organization not found.");

        if (permission.StatusCode == StatusCodes.Status422UnprocessableEntity)
            return Result.Invalid(ValidationError.Create("general", permission.Message ?? "Validation failed."));

        return Result.Forbidden(permission.Message ?? "Access denied.");
    }

    private static User GetCurrentUser(HttpContext httpContext) => httpContext.Request.GetUser();
    private static bool IsStatsMode(string? mode) => !String.IsNullOrEmpty(mode) && String.Equals(mode, "stats", StringComparison.OrdinalIgnoreCase);
    private static bool messageIsGlobalAdmin(HttpContext httpContext) => httpContext.Request.IsGlobalAdmin();
}
