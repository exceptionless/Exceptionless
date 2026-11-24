---
title: "Send your first event"
navTitle: "Quickstart"
metaTitle: "Exceptionless quickstart: send your first event"
description: "Create an Exceptionless account and project, install a client or let your coding agent do it, send a test error, and find it in the app."
sources:
  - src/Exceptionless.Web/ClientApp/src/routes/(auth)/signup/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/auth/redirects.ts
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/+layout.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/organization/add/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/add/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/+layout.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/configure/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/routes.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/projects/components/table/project-actions-cell.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/onboarding/agent-setup.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/onboarding/components/agent-setup-menu.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/onboarding/components/api-key-card.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/onboarding/components/listening-status.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/onboarding/components/platform-picker.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/onboarding/components/platform-instructions.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/onboarding/components/sample-data-button.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/onboarding/components/first-event-celebration.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/onboarding/components/getting-started-checklist.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/onboarding/checklist.ts
  - src/Exceptionless.EmailTemplates/Templates/UserEmailVerify.razor
  - src/Exceptionless.Web/Api/Endpoints/EventEndpoints.cs
  - src/Exceptionless.Web/Api/Handlers/OrganizationHandler.cs
  - src/Exceptionless.Core/Billing/BillingManager.cs
  - src/Exceptionless.Core/Configuration/BillingOptions.cs
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.cs
  - src/Exceptionless.Web/Assistant/AssistantToolDefinitions.cs
  - docs/public/setup.md.txt
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionExtensions.cs
  - Exceptionless.JavaScript:packages/core/src/ExceptionlessClient.ts
---

# Send your first event

In a few minutes you create an Exceptionless account, connect an app, send a test error, and find it in the app. You can
install the client yourself or hand that step to your coding agent.

> [!NOTE]
> Running your own Exceptionless server? Set it up with the [self-hosting guide](/docs/self-hosting/), then follow these
> steps on your own URL instead of `be.exceptionless.io`.

## 1. Create an account

Go to [be.exceptionless.io/signup](https://be.exceptionless.io/signup). Sign up with **Microsoft**, **Google**, or
**GitHub**, or enter your **Name**, **Email**, and **Password** and select **Create account**.

Your account works right away. Exceptionless also emails you a link to confirm your address. Confirm it within 24 hours
to turn on email notifications; you can resend the link from your account's **General** page.

On the hosted service, your first organization starts with a 14-day [free trial](/docs/organizations/#free-trial), with
no credit card, if your account is eligible. Otherwise it starts on Free. See [pricing](/pricing/) for what each plan
includes.

## 2. Name your project and organization

After you sign up, the **Set up Exceptionless** page asks for two names:

- **What's your app called?**: the app you're connecting, such as `Storefront API`. This becomes your first project.
  Each project has its own API key and events.
- **Organization name**: your team or company, filled in with a suggestion you can change. An organization holds your
  projects, users, and plan.

Select **Continue**. Exceptionless creates both and opens **Client Setup** for the new project. Until your project
receives its first event, opening the app brings you back to setup.

To add another project later, go to **Settings** > **Projects** in the sidebar, select **Add Project** (the plus
button), name it, and select **Continue to Client Setup**.

## 3. Install a client

The **Client Setup** page, titled **Send events to** your project name, shows **Listening for your first event…** until
data arrives. Under **Pick your platform**, choose your app's platform or search for it. The page shows the install
command and startup code for that platform with your project's API key already filled in. Copy them into your app. The
**Your API key** card shows the key for your configuration files and environment variables.

The same setup for each client, with `YOUR_API_KEY` in place of your key:

```clients
```

You can come back to this page any time: open **Settings** > **Projects** in the sidebar, open the project's actions
menu, and choose **Client Setup**. The project's keys are on its **API Keys** settings page.

To look around before your app is connected, select **Explore with sample data** to add demo events to the project. You
can clear them later with **Reset project data** in the project's settings.

For framework-specific setup, such as ASP.NET Core, React, or Express, see [Send data](/docs/clients/).

### Or set up with your coding agent

On **Client Setup**, select **Set up with your agent**:

- **Copy setup prompt** copies a prompt to paste into Claude Code, Codex, Copilot, or another coding agent.
- Under **Open in**, **Cursor**, **Claude**, or **ChatGPT** opens that tool with the prompt filled in.

The menu is at the top of the page before you choose a platform, and next to the setup steps after you choose one. The
prompt contains your API key and points the agent at [exceptionless.com/setup.md](https://exceptionless.com/setup.md),
the same setup steps written for agents:

```prompt agent
Fetch and follow the instructions at https://exceptionless.com/setup.md to set up Exceptionless for me. My API key is YOUR_API_KEY.
```

When **Client Setup** shows a server URL, such as on a self-hosted server, the prompt includes it too. The agent works
out your app's platform, installs and configures the client, and sends a test error, `Hello from Exceptionless setup`.
For agent skills and more agent workflows, see [Coding agents](/docs/ai/coding-agents/).

## 4. Send a test error

Once the client is installed, unhandled errors are sent automatically. To check the setup right away, submit a handled
error from your app:

```csharp tab=".NET"
using Exceptionless;

try {
    throw new InvalidOperationException("Exceptionless test error");
} catch (Exception ex) {
    ex.ToExceptionless().Submit();
}
```

```ts tab="JavaScript"
// Use the package you installed: @exceptionless/browser, @exceptionless/node, and so on.
import { Exceptionless } from "@exceptionless/node"

try {
  throw new Error("Exceptionless test error")
} catch (error) {
  await Exceptionless.submitException(error as Error)
}
```

```bash tab="curl"
curl "https://api.exceptionless.io/api/v2/events" \
  --request POST \
  --header "Authorization: Bearer YOUR_API_KEY" \
  --header "Content-Type: application/json" \
  --data-binary '{"type":"error","@simple_error":{"message":"Exceptionless test error","type":"System.Exception","stack_trace":"   at Quickstart.Main() in Program.cs:line 1"}}'
```

Clients queue events and send them in the background, so a test error can take a few seconds to arrive. In a console
app, script, or serverless function that exits right away, send the queue before it exits: in .NET,
`await ExceptionlessClient.Default.ProcessQueueAsync()` sends until the queue is empty or a send fails. In JavaScript,
`await Exceptionless.processQueue()` sends one batch of up to 50 events.

> [!NOTE]
> On a self-hosted server, use your own API URL instead of `https://api.exceptionless.io`, and set the client's server
> URL as shown on your **Client Setup** page.

## 5. Find it in Exceptionless

If **Client Setup** is open when the first event arrives, a **You caught your first event!** dialog shows it. Select
**Take me to it** to open the event, or **See all events** to open **Events** for your project. Otherwise, select
**Events** or **Stacks** in the sidebar.

- **Events** lists each occurrence. Select the test error to open its details; the **Exception** tab shows the stack
  trace.
- **Stacks** groups repeats of the same error into one stack, with its event count, first and last occurrence, and
  status.

Once you've seen the test error, set its stack's status to **Fixed** or **Discarded** so it doesn't stay in your open
errors. [Manage stacks](/docs/managing-stacks/) explains each status.

Nothing showing up? After a short wait, **Client Setup** shows **Not seeing anything?** with tips for your platform.
Check that the API key matches the project and is enabled on the project's **API Keys** page, then see troubleshooting
for [.NET](/docs/clients/dotnet/troubleshooting/) or [JavaScript](/docs/clients/javascript/troubleshooting/).

## Next steps

For the first 30 days of a new organization, **Exie's checklist** on **Stacks** and **Events** tracks the next steps,
such as verifying your email, reviewing notifications, and inviting your team. Dismiss it when you're done.

- **Connect your coding agent.** The [MCP server](/docs/ai/mcp-server/), available on every plan, lets an agent such as
  Claude Code or Codex look up your stacks and events and update them while it works in your code. Choose **AI Tools**
  in your user menu, or **Set Up AI Tools** on **Client Setup**, for the setup commands for your agent.
- **Ask Exie.** [Exie](/docs/ai/exie/) is the assistant built into the app. Open it with **Ask Exie** in the top bar or
  press <kbd>A</kbd>. Exie is on Medium plans and up, and during a free trial once you verify your email.
- **Learn the model.** [Core concepts](/docs/concepts/) explains events, stacks, environments, and saved views.
- **Get alerted.** Set up [notifications](/docs/notifications/) for new errors and regressions.
- **Send more.** Logs, feature usage, and user context are covered in your client's [sending events](/docs/clients/)
  guide.

## Ask Exie

```prompt exie
Did my new project receive any errors today?
```

```prompt exie
Explain the "Exceptionless test error" stack and show me its latest event.
```

```prompt exie
Mark the "Exceptionless test error" stack as fixed.
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can check the setup for you:

```prompt agent
Check that my Exceptionless project received the "Exceptionless test error" event. If it did, mark that stack fixed.
```
