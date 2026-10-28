---
title: "Your account"
description: "Manage your Exceptionless profile, theme, email notifications, AI tool connections, connected applications, sign-in methods, and password."
navTitle: "Your account"
sources:
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/account/routes.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/account/manage/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/account/appearance/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/account/notifications/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/account/ai-tools/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/account/applications/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/account/external-logins/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/account/security/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/(components)/layouts/sidebar-user.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/users/gravatar.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/users/components/dialogs/delete-current-user-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/users/components/oauth-grants/table/options.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/users/components/oauth-grants/table/oauth-grant-access-cell.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/users/components/oauth-grants/table/oauth-grant-actions-cell.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/auth/index.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/auth/schemas.ts
  - src/Exceptionless.Web/Api/Handlers/UserHandler.cs
---

# Your account

Your account settings cover your profile, how the app looks, your email notifications, the AI tools and applications
connected to your account, how you sign in, and deleting your account.

To open them, click your name at the bottom of the sidebar (or press <kbd>U</kbd>) and choose **Account**. The **My
Account** pages are **General**, **Appearance**, **Notifications**, **AI Tools**, **Applications**, **Password**, and
**External Logins**.

## Profile

On the **General** page:

- **Avatar:** click the picture to upload one (PNG, JPEG, GIF, or WebP). The **X** button next to it removes it. Without
  an upload, the app uses your [Gravatar](https://gravatar.com/) for your email address, or your initials.
- **Full Name:** saves as you type.
- **Email:** saves as you type, once the app confirms no other account uses the address. You can change it up to three
  times an hour.

When you change your email address, you need to verify the new one, unless it matches the email address of one of your
external logins. Until you verify, Exceptionless doesn't send you email notifications. If your address isn't verified,
the page shows **Email not verified** with a link to resend the verification email.

## Appearance

On the **Appearance** page, choose **Light**, **Dark**, or **System**. **System** follows your operating system's
setting. To switch quickly, press <kbd>/</kbd> and choose **Toggle Theme**.

## Email notifications

The **Notifications** page turns all of your notification emails on or off with **Enable Email Notifications**, and sets
which emails you get for each project. See [Notifications](/docs/notifications/).

## AI Tools

The **AI Tools** page has setup commands for connecting a coding agent to the Exceptionless
[MCP server](/docs/ai/mcp-server/). Choose **Claude Code**, **Codex**, **Copilot**, or **OpenCode**, then run each
command in order. The commands already contain your instance's server URL. When the agent connects, you approve its
access in the browser.

The page ends with **Try asking**, a list of prompts to try once your agent is connected.

## Connected applications {#connected-applications}

The **Applications** page lists every application you've given access to your account, such as a coding agent connected
to the MCP server. For each one you see:

- **Application:** its name and when its access was last updated. **Disabled** means an administrator turned the
  application off.
- **Access:** what it can reach (**MCP** or **REST API**) and how many scopes it has. Hover to see the scopes, such as
  **Projects**, **Stacks**, **Events**, **Stacks Write**, and **Offline**.
- **Organizations:** the organizations it can use.

To disconnect an application, open its menu (**...**), choose **Revoke Access**, and confirm. It has to sign in and get
your approval again before it can use your account. See
[Manage connected applications](/docs/ai/mcp-server/#connected-applications) for what each scope allows.

## External logins

External logins let you sign in with another account instead of a password. On the **External Logins** page:

- Under **Add an external login**, click **Microsoft**, **Google**, or **GitHub** to link that account.
- Under **Existing external logins**, click **X** to unlink one.

You can't remove your only way to sign in. If you have no password and one external login, set a password first.

Only the providers your Exceptionless instance has set up appear. Older Facebook and Windows Live logins can no longer
be used to sign in; link Microsoft, Google, or GitHub, or set a password, to keep access. If no provider is set up, the
page says **External login providers are not configured.**

## Password

On the **Password** page:

- If you have a password, enter your **Current Password**, a **New Password**, and **Confirm Password**, then click
  **Change Password**.
- If you've only signed in with external logins, enter a **Password** and **Confirm Password**, then click **Set
  Password**.

Passwords must be 6 to 100 characters. If you forgot your password, sign out and use the **Forgot password?** link on
the sign-in page.

## Delete your account

1. Leave or delete every organization you belong to. See
   [Organizations and billing](/docs/organizations/#users-and-invites).
2. On the **General** page, click **Delete Account**, then **Delete Account** again to confirm.

Your account is queued for deletion and you're signed out. Your API tokens and connected applications are removed with
it. If you still belong to an organization, the app shows **Please delete or leave any organizations before deleting
your account.**

## Ask Exie

Exie works with your projects, stacks, and events, not your account settings. Once you've connected a coding agent on
the **AI Tools** page, check that it works:

```prompt agent
List my Exceptionless projects and the newest error in each one.
```
