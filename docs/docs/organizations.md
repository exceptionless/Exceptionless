---
title: "Organizations and billing"
description: "Create organizations, invite users, change plans, check usage, and view invoices for your Exceptionless organization."
navTitle: "Organizations"
sources:
  - docs/_data/plans.json
  - src/Exceptionless.Core/Billing/BillingPlans.cs
  - src/Exceptionless.Core/Billing/BillingManager.cs
  - src/Exceptionless.Core/Authorization/AuthorizationRoles.cs
  - src/Exceptionless.Core/Services/OrganizationService.cs
  - src/Exceptionless.Web/Api/Handlers/OrganizationHandler.cs
  - src/Exceptionless.Web/Api/Handlers/AuthHandler.cs
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/organization/routes.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/organization/[organizationId]/routes.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/organization/[organizationId]/manage/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/organization/[organizationId]/users/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/organization/[organizationId]/billing/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/organization/[organizationId]/usage/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/organization/add/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/organization/list/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/payment/[id]/+page@.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/(components)/layouts/sidebar-user.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/(components)/layouts/sidebar-organization-switcher.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/billing/components/change-plan-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/billing/invoice.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/users/components/invite-user-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/users/components/table/options.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/users/components/table/user-actions-cell.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/organizations/components/table/organization-actions-cell.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/organizations/components/notifications/free-plan-notification.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/organizations/components/notifications/monthly-overage-notification.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/organizations/components/notifications/hourly-overage-notification.svelte
---

# Organizations and billing

An organization holds your projects, the users who can see them, and the plan you pay for. Plan limits, such as events
per month and the number of users, apply to the whole organization. This page covers organization settings, users,
plans, usage, and invoices.

## Open organization settings

Any of these opens the current organization's settings:

- the user menu at the bottom of the sidebar (<kbd>U</kbd>) > **Manage Organization** or **Billing**,
- the organization switcher at the top of the sidebar (<kbd>O</kbd>) > **Manage Organization**,
- **Settings** > **Organizations** in the sidebar, then click the organization.

The settings have these tabs: **General**, **Users**, **Billing**, and **Usage**.

To switch to another organization, use the organization switcher, or press <kbd>/</kbd> and type its name. See
[Navigating the app](/docs/navigation/).

## Create an organization

1. Open the organization switcher and click **Add Organization**, or click **+** (**Add Organization**) on **Settings**
   > **Organizations**.
2. Enter an **Organization Name** and a **Project Name**, then click **Continue**.
3. Connect your app on the **Client Setup** page that opens. See [Quickstart](/docs/getting-started/).

New organizations start on the Free plan. You can belong to only one organization on the Free plan, so to create another
organization, upgrade the Free one first.

## Change organization settings

On the **General** tab:

- Click the icon to upload an organization icon (PNG, JPEG, GIF, or WebP). The **X** button next to it removes it.
- Edit the **Organization name**. It saves as you type.
- **View Events** and **View Projects** open the organization's events and projects.

## Users and invites {#users-and-invites}

Everyone in an organization has the same access: they can see all of its projects and events, change settings, invite
and remove users, and manage billing. There are no per-user roles, so invite only people you trust with all of that. To
give a tool limited access instead, use a [client API key](/docs/project-settings/#api-keys) or a
[connected application](/docs/account/#connected-applications).

To invite someone:

1. Open the **Users** tab and click **+** (**Invite User**). You can also choose **Invite User** in the command palette.
2. Enter their **Email Address** and click **Invite User**.

If the email address already has an Exceptionless account, that user joins the organization right away and gets an email
about it. Otherwise, Exceptionless emails them an invitation, and they join when they sign up from it. Pending
invitations show in the list with the date in the **Invited** column.

From a user's menu (**...**) in the list:

- **Resend Invite Email** sends a pending invitation again.
- **Revoke Invite** cancels a pending invitation.
- **Remove User** takes a member out of the organization. Their private [saved views](/docs/saved-views/) and their
  [notification settings](/docs/notifications/) for the organization's projects are deleted.

Pending invitations count toward your plan's user limit. An organization always keeps at least one user.

To leave an organization yourself, open **Settings** > **Organizations**, open the organization's menu (**...**), and
click **Leave Organization**.

## Plans

| Plan         | Includes                                                                                                               |
| ------------ | ---------------------------------------------------------------------------------------------------------------------- |
| Free         | Error and log monitoring, the daily project summary, and the MCP server.                                               |
| Small        | Everything in Free, plus event notifications by email and Slack, webhooks, sessions, and filtering on any event field. |
| Medium       | Everything in Small, plus [Exie](/docs/ai/exie/).                                                                      |
| Large and up | Everything in Medium, with unlimited projects and users.                                                               |

Plans differ in events per month, data retention, and the number of projects and users. See
[Limits](/docs/concepts/limits/) for the numbers and what happens when you reach them, and [pricing](/pricing/) for
prices. Yearly billing is available for every paid plan and costs the same as 11 months.

When the organization reaches a limit, the app shows a banner, such as **has reached its monthly plan limit** or
**Events are currently being throttled**, with an **Upgrade now** link.

## Change your plan

1. Open the **Billing** tab and click **Change your plan or billing information**. The **Manage subscription** dialog
   opens. **Upgrade now** links in the app open the same dialog.
2. Choose **Monthly** or **Yearly**, then choose a plan. Choose **Free** to cancel your paid plan.
3. For a paid plan, enter a card under **Payment method**, or keep the card on file.
4. To use a coupon, click **Have a coupon code?**, enter it, and click **Apply**.
5. Click the button at the bottom, such as **Start Small monthly**, **Switch to Large**, or **Downgrade to Free**.

Plan changes take effect right away and are prorated: you pay the difference when you upgrade, and get a prorated credit
when you downgrade. To update only your card, choose **Use a different payment method**, enter it, and click **Update
payment method**.

You can't move to a plan that allows fewer users or projects than you have. The dialog tells you how many to remove
first, and pending invitations count as users.

## Usage

The **Usage** tab shows how many events the organization has left this billing period and when the count resets. The
chart shows **Total** events against the **Limit**, along with **Blocked**, **Discarded**, **Too Big**, and **Deleted**
events. Each project's settings have a **Usage** tab too. See [Limits](/docs/concepts/limits/) for what each line means.

## Invoices

The **Billing** tab lists your payments with their **Payment Number**, **Date**, **Amount**, and **Status**, such as
**Paid**, **Payment due**, or **Credit issued**. Click a payment, or choose **View Payment** from its menu, to open a
receipt that you can print or save.

## Delete an organization

1. Delete the organization's projects first. See [Projects](/docs/project-settings/#delete-a-project).
2. On the **General** tab, click **Delete** > **Delete Organization**, and confirm.

You can't delete an organization that still has projects. You also can't delete one that has had a paid subscription:
the app shows **An organization cannot be deleted if it has a subscription.** Contact support to close it.

## Self-hosting

Self-hosted instances usually run without billing. New organizations then get an unlimited plan, the **Billing** tab has
no plan to change, and the **Usage** tab says monthly usage isn't available. See [Self-hosting](/docs/self-hosting/).

## Ask Exie

[Exie](/docs/ai/exie/) is on Medium plans and up.

```prompt exie
List the projects in this organization and when each one last received an event.
```

```prompt exie
How many events did this project receive each day this week?
```
