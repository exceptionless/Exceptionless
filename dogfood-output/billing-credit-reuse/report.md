# Billing credit reuse dogfood

Date: 2026-08-03

## Scope

- Local headed-browser flow against the current worktree and Stripe test mode.
- Fresh synthetic account and organization: `Billing Credit Dogfood 2026-08-03` (`6a710ecd838d016bb127ff04`).
- Flow: Free -> Small yearly -> Free -> Small yearly.

## Result

PASS. The cancellation proration became customer credit and was fully applied to the next subscription invoice. Stripe retained one canceled historical subscription and exactly one live subscription.

| Step | Invoice | UI amount/status | Stripe evidence |
| --- | --- | --- | --- |
| Initial Small yearly | `in_0U0TxG462kw6hVF0MM2cq7c3` | $165.00 / Paid | `amount_due=16500`, `amount_paid=16500`, `starting_balance=0` |
| Downgrade to Free | `in_0U0Txt462kw6hVF0SrdIuAMp` | -$165.00 / Credit issued | `total=-16500`, `ending_balance=-16500`, `status=paid` |
| Return to Small yearly | `in_0U0TyB462kw6hVF0tsBPaH3H` | $165.00 / Paid | `starting_balance=-16500`, `amount_due=0`, `amount_paid=0`, `ending_balance=0` |

The final Stripe customer balance was zero. Subscription state was:

- `sub_0U0TyB462kw6hVF011Tcxxu6`: active
- `sub_0U0TxG462kw6hVF0HMbADWm1`: canceled historical record
- Live subscription count: 1

## Runtime observations

- All three plan-change requests returned HTTP 200.
- The billing table correctly distinguished the negative paid invoice as `Credit issued` rather than `Unpaid`.
- The billing table displays the invoice gross total ($165.00) for the re-upgrade. Stripe provider fields prove no second card charge occurred because the credit reduced `amount_due` and `amount_paid` to zero.
- AppHost did not forward Stripe configuration from its own environment to the API/Vite child resources. Temporary local-only forwarding was used for this dogfood run and then removed from the reviewed source diff; no secret values were stored in source.

## Evidence

- `16-initial-paid.png`: first paid invoice
- `17-credit-issued.png`: finalized cancellation credit
- `18-final-invoices.png`: final three-invoice view left open in the headed browser
