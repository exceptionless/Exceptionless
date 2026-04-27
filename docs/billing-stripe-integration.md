# Billing & Stripe Integration

## Overview

Exceptionless uses [Stripe](https://stripe.com) for subscription billing. The integration spans:

- **Backend**: ASP.NET Core controller (`OrganizationController`) + Stripe.net SDK v51
- **Frontend**: Svelte 5 dialog (`ChangePlanDialog`) + Stripe.js v9 PaymentElement
- **Legacy**: Angular app supports `tok_` tokens via `createToken()` (backwards compatible)

## Architecture

```text
┌─────────────────┐     ┌──────────────────┐     ┌─────────┐
│  Svelte Dialog   │────>│  /change-plan    │────>│ Stripe  │
│  (PaymentElement)│     │  Controller      │     │   API   │
│  pm_ tokens      │     │  (Stripe.net 51) │     │         │
├─────────────────┤     │                  │     │         │
│  Angular Dialog  │────>│  Detects pm_ vs  │────>│         │
│  (createToken)   │     │  tok_ prefix     │     │         │
│  tok_ tokens     │     └──────────────────┘     └─────────┘
└─────────────────┘
```

## Configuration

### Environment Variables

| Variable | Where | Purpose |
| --- | --- | --- |
| `StripeApiKey` | Server (`AppOptions.StripeOptions`) | Secret API key for server-side Stripe calls |
| `PUBLIC_STRIPE_PUBLISHABLE_KEY` | Svelte (`ClientApp/.env.local`) | Publishable key for Stripe.js |
| `STRIPE_PUBLISHABLE_KEY` | Angular (`app.config.js`) | Publishable key for legacy UI |

The server-side key is injected via environment variables or `appsettings.Local.yml` (gitignored).

### Enabling Billing

Billing is enabled when `StripeApiKey` is configured. The frontend checks `isStripeEnabled()` (reads `PUBLIC_STRIPE_PUBLISHABLE_KEY`). If not set, the dialog shows "Billing is currently disabled."

## API Endpoints

### `GET /api/v2/organizations/{id}/plans`

Returns available billing plans for the organization. The current org's plan entry is replaced with runtime billing values (custom pricing, limits).

**Auth**: `UserPolicy`  
**Response**: `BillingPlan[]`

### `POST /api/v2/organizations/{id}/change-plan`

Changes the organization's billing plan.

**Auth**: `UserPolicy` + `CanAccessOrganization(id)`  
**Body** (JSON, preferred):

| Field | Type | Description |
| --- | --- | --- |
| `plan_id` | string | Target plan ID (e.g., `EX_MEDIUM`, `EX_LARGE_YEARLY`) |
| `stripe_token` | string? | `pm_` PaymentMethod ID (Svelte) or `tok_` token (Angular) |
| `last4` | string? | Last 4 digits of card (display only) |
| `coupon_id` | string? | Stripe coupon code |

Legacy Angular clients may pass these as query string parameters instead.

**Response**: `ChangePlanResult { success, message }`

**Behavior**:

1. If no `StripeCustomerId` → creates Stripe customer + subscription
2. If existing customer → updates customer + subscription
3. `pm_` tokens use `PaymentMethod` API; `tok_` tokens use legacy `Source` API
4. Coupons applied via `SubscriptionDiscountOptions` (Stripe.net 50.x+)

### `GET /api/v2/organizations/invoice/{id}`

Returns a single invoice with line items.

**Auth**: `UserPolicy` + `CanAccessOrganization`  
**Response**: `Invoice { id, organization_id, organization_name, date, paid, total, items[] }`

### `GET /api/v2/organizations/{id}/invoices`

Returns paginated invoice grid for the organization.

**Auth**: `UserPolicy`  
**Response**: `InvoiceGridModel[]`

## Plan Structure

Plans follow a tiered naming convention:

| Tier | Monthly ID | Yearly ID |
| --- | --- | --- |
| Free | `EX_FREE` | — |
| Small | `EX_SMALL` | `EX_SMALL_YEARLY` |
| Medium | `EX_MEDIUM` | `EX_MEDIUM_YEARLY` |
| Large | `EX_LARGE` | `EX_LARGE_YEARLY` |
| Extra Large | `EX_XL` | `EX_XL_YEARLY` |
| Enterprise | `EX_ENT` | `EX_ENT_YEARLY` |

The frontend groups monthly/yearly variants into "tiers" for the UI. The `_YEARLY` suffix determines the billing interval.

## Frontend Components

### `ChangePlanDialog`

Main billing dialog at `src/lib/features/billing/components/change-plan-dialog.svelte`.

**Props**:

- `organization: ViewOrganization` — current org data
- `onclose: (success: boolean) => void` — callback when dialog closes
- `initialCouponCode?: string` — pre-fill coupon
- `initialCouponOpen?: boolean` — open coupon input on mount
- `initialFormError?: string` — show error message on mount

**Features**:

- Tile-based plan selection with Monthly/Yearly tabs
- "Save X%" badge computed from tier pricing differences
- "MOST POPULAR" badge on XL tier (only for upgrades)
- Stripe PaymentElement for new payment methods
- "Keep current card" / "Use a different payment method" toggle
- Coupon input with apply/remove
- Footer summary showing plan change, payment, and coupon details
- Destructive (red) CTA for downgrades, default (green) for upgrades
- Disabled CTA when no changes
- Form validation via TanStack Form + Zod (`ChangePlanSchema`)
- Error reporting via Exceptionless client

### `StripeProvider`

Imperative Stripe.js loader at `src/lib/features/billing/components/stripe-provider.svelte`.

Uses direct DOM manipulation instead of svelte-stripe's `<Elements>` / `<PaymentElement>` due to a Svelte 5 reactivity issue where `$state` set from async callbacks doesn't reliably trigger template re-renders.

### `UpgradeRequiredDialog`

Handles 426 responses with "Upgrade Plan" / "Cancel" buttons. Mounted in the app layout.

### `showBillingDialogOnUpgradeProblem(error, organizationId, retryCallback?)`

Utility used across 6 route pages to intercept `ProblemDetails` with `status: 426` and open the upgrade dialog. No-ops when the error is not a 426.

## Stripe SDK Migration Notes (v47 → v51)

### Stripe API Changes Handled

1. **`Invoice.Paid` removed** → Use `String.Equals(invoice.Status, "paid", StringComparison.Ordinal)`
2. **`Invoice.Discount` removed** → Use `Invoice.Discounts?.FirstOrDefault(d => d.Deleted is not true)?.Source?.Coupon`
3. **`line.Plan` removed** → Use local `_billingManager.GetBillingPlan()` lookup
4. **`CustomerCreateOptions.Plan` removed** → Create subscription separately
5. **`CustomerCreateOptions.Coupon` removed** → Use `SubscriptionDiscountOptions`
6. **`SubscriptionItemOptions.Plan` removed** → Use `SubscriptionItemOptions.Price`
7. **`CustomerCreateOptions.Source`** → Use `CustomerCreateOptions.PaymentMethod` for `pm_` tokens

### Backwards Compatibility

The `tok_` token path (Angular legacy UI) is preserved. The controller detects `pm_` vs `tok_` prefix and routes to the appropriate Stripe API:

```csharp
bool isPaymentMethod = stripeToken?.StartsWith("pm_", StringComparison.Ordinal) is true;
```

## Known Limitations

1. ~~**Coupon not applied for existing customers changing plans**~~ — Fixed. Coupons are now applied in all paths: new customer, existing customer updating subscription, and existing customer creating a new subscription.
2. ~~**Potential orphaned Stripe customers**~~ — Fixed. `StripeCustomerId` is now persisted immediately after customer creation (before subscription creation), so a retry will reuse the existing customer.
3. ~~**N+1 price fetches in invoice view**~~ — N/A. Invoice line items are resolved against the local billing plan registry (`_billingManager.GetBillingPlan`) rather than fetching Stripe Price objects.
4. **svelte-stripe package unused** — Listed in `package.json` but bypassed due to Svelte 5 incompatibility. Only `@stripe/stripe-js` is used directly.

## Storybook

15 stories cover all dialog states:

| Story | Description |
| --- | --- |
| Error loading plans | Plans query failed |
| Default | Free plan org, Small pre-selected |
| Change plan | Small → Medium upgrade |
| Interval switch | Monthly → Yearly toggle |
| Update card only | Change payment method without plan change |
| Apply coupon only | Apply coupon without plan change |
| Plan + card + coupon | All three changes |
| First-time paid | Free → first paid plan |
| Downgrade to Free | Cancel paid plan |
| Coupon input open | Coupon text field visible |
| Coupon applied | Coupon alert shown |
| Error: invalid coupon | Bad coupon with form error |
| Error: payment failed | Payment declined error |
| Error: downgrade blocked | Downgrade blocked by limits |
| Error: plan change failed | Generic plan change error |

Run stories: `cd src/Exceptionless.Web/ClientApp && npm run storybook`

## Security Considerations

- **Server-side Stripe API key** is never exposed to the client. Only the publishable key is used in the frontend.
- **All billing endpoints require `UserPolicy` auth** and `CanAccessOrganization` access checks.
- **Token/PaymentMethod IDs are validated by Stripe** server-side — no additional format validation needed.
- **Coupon codes are validated by Stripe** — no injection risk.
- **No PII in logs** — only invoice IDs, price IDs, and plan IDs are logged.
