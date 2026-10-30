---
title: "Send your first event"
navTitle: "Quickstart"
metaTitle: "Exceptionless quickstart: send your first event"
description: "Create an Exceptionless account and project, install a client or let your coding agent do it, send a test error, and find it in the app."
sources:
  - src/Exceptionless.Web/ClientApp/src/routes/(auth)/signup/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/organization/add/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/add/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/configure/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/routes.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/projects/components/table/project-actions-cell.svelte
  - src/Exceptionless.Web/Api/Endpoints/EventEndpoints.cs
  - src/Exceptionless.Web/Api/Handlers/OrganizationHandler.cs
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.cs
  - src/Exceptionless.Web/Assistant/AssistantToolDefinitions.cs
  - Exceptionless.Net:.agents/skills/exceptionless-dotnet/SKILL.md
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionExtensions.cs
  - Exceptionless.JavaScript:.agents/skills/exceptionless-javascript/SKILL.md
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
**GitHub**, or enter your **Name**, **Email**, and **Password** and select **Create My Account**.

New organizations start on the Free plan. See [pricing](/pricing/) for what each plan includes.

## 2. Create an organization and a project

After you sign up, the **Set Up Exceptionless** page asks for two names:

- **Organization Name**: your team or company. An organization holds your projects, users, and plan.
- **Project Name**: the app you're connecting, such as `Storefront API`. Each project has its own API key and events.

Select **Continue**. Exceptionless creates both and opens **Client Setup** for the new project.

To add another project later, go to **Settings** > **Projects** in the sidebar and select **Add Project** (the plus
button).

## 3. Install a client

The **Client Setup** page, titled **Send Events to** your project name, shows **Waiting for your first event** while it
listens for data. Choose your project type from the list, and the page shows the install command and startup code with
your project's API key already filled in. Copy them into your app.

The same setup for each client, with `YOUR_API_KEY` in place of your key:

```clients
```

You can come back to this page any time: open **Settings** > **Projects** in the sidebar, open the project's actions
menu, and choose **Client Setup**. The project's keys are on its **API Keys** settings page.

For framework-specific setup, such as ASP.NET Core, React, or Express, see [Send data](/docs/clients/).

### Or install with your coding agent

Paste this prompt into Claude Code, Codex, Copilot, or another coding agent, and replace `YOUR_API_KEY` with the key
from **Client Setup**:

```prompt agent
Add Exceptionless error reporting to this app. My Exceptionless API key is YOUR_API_KEY.

1. Work out the app's language and framework. Then read the matching Exceptionless setup guide as Markdown. The client list is at https://exceptionless.com/docs/clients.md, and each guide has a .md URL, for example https://exceptionless.com/docs/clients/dotnet/aspnet-core.md for ASP.NET Core.
2. If you can use agent skills, follow the official Exceptionless skill for the language: https://github.com/exceptionless/Exceptionless.Net/tree/main/.agents/skills/exceptionless-dotnet for .NET, or https://github.com/exceptionless/Exceptionless.JavaScript/tree/main/.agents/skills/exceptionless-javascript for JavaScript.
3. Install the right package for the app type and configure it at startup. Put the API key in the app's configuration, not inline in code, where the framework supports it.
4. Send one test error so I can check that it arrives in Exceptionless, then tell me how to remove the test code.
```

The agent skills are part of the [Exceptionless.Net](https://github.com/exceptionless/Exceptionless.Net) and
[Exceptionless.JavaScript](https://github.com/exceptionless/Exceptionless.JavaScript) repositories. For more agent
workflows, see [Coding agents](/docs/ai/coding-agents/).

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
app, script, or serverless function that exits right away, flush the queue before it exits: call
`await ExceptionlessClient.Default.ProcessQueueAsync()` in .NET or `await Exceptionless.processQueue()` in JavaScript.

> [!NOTE]
> On a self-hosted server, use your own API URL instead of `https://api.exceptionless.io`, and set the client's server
> URL as shown on your **Client Setup** page.

## 5. Find it in Exceptionless

If **Client Setup** is still open from step 2, it opens **Events** for your project as soon as the first event arrives.
Otherwise, select **Events** or **Stacks** in the sidebar.

- **Events** lists each occurrence. Select the test error to open its details; the **Exception** tab shows the stack
  trace.
- **Stacks** groups repeats of the same error into one stack, with its event count, first and last occurrence, and
  status.

Once you've seen the test error, set its stack's status to **Fixed** or **Discarded** so it doesn't stay in your open
errors. [Manage stacks](/docs/managing-stacks/) explains each status.

Nothing showing up? Check that the API key matches the project and is enabled on the project's **API Keys** page, then
see troubleshooting for [.NET](/docs/clients/dotnet/troubleshooting/) or
[JavaScript](/docs/clients/javascript/troubleshooting/).

## Next steps

- **Connect your coding agent.** The [MCP server](/docs/ai/mcp-server/), available on every plan, lets an agent such as
  Claude Code or Codex look up your stacks and events and update them while it works in your code. Choose **AI Tools**
  in your user menu for the setup commands for your agent.
- **Ask Exie.** [Exie](/docs/ai/exie/) is the assistant built into the app. Open it with **Ask Exie** in the top bar or
  press <kbd>A</kbd>. Exie is on Medium plans and up.
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
