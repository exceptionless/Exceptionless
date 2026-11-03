---
title: "AI features"
metaTitle: "AI features for self-hosted Exceptionless"
description: "Turn on Exie with a model provider key, choose the model, connect coding agents to the built-in MCP server, and turn AI off."
sources:
  - src/Exceptionless.Core/Configuration/AssistantOptions.cs
  - src/Exceptionless.Core/Configuration/OAuthServerOptions.cs
  - src/Exceptionless.Core/Billing/BillingPlans.cs
  - src/Exceptionless.Core/Services/OAuthService.cs
  - src/Exceptionless.Core/Services/SystemSettingsService.cs
  - src/Exceptionless.Web/Assistant/AssistantAccessService.cs
  - src/Exceptionless.Web/Assistant/AssistantModelSettingsService.cs
  - src/Exceptionless.Web/Assistant/AssistantService.cs
  - src/Exceptionless.Web/Assistant/AssistantLimits.cs
  - src/Exceptionless.Web/Mcp/McpOriginValidationMiddleware.cs
  - src/Exceptionless.Web/Api/Endpoints/OAuthEndpoints.cs
  - src/Exceptionless.Web/Api/Handlers/OAuthHandler.cs
  - src/Exceptionless.Web/ClientApp/src/lib/features/admin/components/assistant-settings.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/admin/schemas.ts
  - k8s/exceptionless/templates/config.yaml
---

# AI features

Self-hosted Exceptionless has two AI features. [Exie](/docs/ai/exie/), the assistant in the app, calls a model provider
with your API key. The [MCP server](/docs/ai/mcp-server/) lets coding agents such as Claude Code and Codex use your data
with their own model, and works without any provider key. This page covers setting up both, and turning them off.

## Turn on Exie

1. Create an API key at [OpenRouter](https://openrouter.ai/). Exie uses OpenRouter by default.
2. Set the key on the containers that serve the API, such as `app`:

   ```yaml
   EX_Assistant__ApiKey: YOUR_OPENROUTER_API_KEY
   ```

   Setting a key turns Exie on, unless you also set `EX_Assistant__Enabled` to `false`.

3. Restart the containers. Exie appears in the app for organizations whose plan includes it.

Organizations created on a self-hosted instance without billing are on the Unlimited plan, which includes Exie. The
server needs outbound HTTPS access to the provider.

When someone asks Exie a question, Exceptionless sends the conversation and the results of Exie's tools, which include
event and stack data from your projects, to the provider. See [AI privacy and security](/docs/ai/privacy/).

## Choose the model {#choose-the-model}

Exie uses `openai/gpt-6.1-sol` unless you choose another model. There are two ways to change it:

- **When you deploy:** set `EX_Assistant__Model` to an OpenRouter model ID, such as `provider/model`.
- **At runtime:** as a global administrator, go to **Settings** > **System** > **Settings**, enter a model ID under
  **Exie model**, and select **Save**. The change applies to new conversations and turns without a restart. **Reset**
  returns to the deployed model.

The model must support tool calling, because Exie answers questions by calling tools.

To use a provider other than OpenRouter, set `EX_Assistant__Endpoint` to its chat completions URL. The endpoint must
accept OpenAI-style streaming chat completion requests with tools and a bearer API key. Exceptionless is built and
tested against OpenRouter, so other endpoints may need changes.

## Usage limits and cost

Each organization's plan limits how much it can use Exie. On the Unlimited plan, an organization can run 20 turns at
once and 100 turns a minute, and use up to 500 million tokens and $100 of provider cost a month. When it reaches a
monthly limit, Exie stops answering for that organization until the next month.

Exceptionless records the cost the provider reports for each turn. If the provider doesn't report a cost, Exceptionless
estimates it at $2 per million prompt tokens and $8 per million completion tokens. To see usage and cost by
organization, go to **Settings** > **System** > **Exie**. See
[Administration](/docs/self-hosting/administration/#exie-usage).

## Turn off Exie

Do any of these:

- Go to **Settings** > **System** > **Settings**, turn off **Exie availability**, and select **Save**. This applies to
  every organization right away, without a restart. **Reset** returns to the deployed setting.
- Set `EX_Assistant__Enabled` to `false` and restart.
- Remove `EX_Assistant__ApiKey`. Without a key, Exie can't be turned on at runtime.

## Set up the MCP server

The MCP server is built in at `/mcp` on your API URL: `EX_ApiUrl` if you set it, otherwise `EX_BaseURL`. For example,
`https://errors.example.com/mcp`. Agents sign in with OAuth, so a person approves each connection and chooses which
organizations and scopes it gets. For connecting each agent, see [MCP server](/docs/ai/mcp-server/).

```bash
claude mcp add --transport http exceptionless https://errors.example.com/mcp
```

For the MCP server to work:

- **Set the public URLs.** OAuth discovery at `/.well-known/oauth-protected-resource/mcp` and
  `/.well-known/oauth-authorization-server` advertises the origin of `EX_ApiUrl`, or of `EX_BaseURL` when `EX_ApiUrl`
  isn't set. Agents must be able to reach that origin. People approve connections on the web app at `EX_BaseURL`.
- **Trust your reverse proxy.** Client registration is limited per IP address, so set up
  [forwarded headers](/docs/self-hosting/reverse-proxy/) when a proxy is in front.
- **Allow browser-based clients.** Requests to `/mcp` from a browser carry an `Origin` header. The origins of
  `EX_BaseURL` and `EX_ApiUrl` are always allowed. Add others as `EX_Mcp__AllowedOrigins__0`,
  `EX_Mcp__AllowedOrigins__1`, and so on. Other origins get a 403. Agents that run outside a browser don't send an
  origin and aren't affected.
- **Make it reachable for hosted clients.** Agents that run on your machine can use a private or `localhost` URL.
  Clients that run on a provider's servers can connect only if your instance is reachable from the internet over HTTPS.

Agents register themselves in one of two ways, and both are on by default:

- **Dynamic client registration.** The agent registers through `/api/v2/oauth/register`. Each IP address can make 20
  registration attempts an hour; change it with `EX_OAuthServer__DynamicClientRegistrationIpLimit`.
- **Client ID metadata documents.** The agent uses an HTTPS URL as its client ID, and Exceptionless downloads the
  client's details from it. This needs outbound HTTPS access. Turn it off with
  `EX_OAuthServer__EnableClientIdMetadataDocuments=false`.

Global administrators can also create clients by hand under **Settings** > **System** > **OAuth Apps**. Token lifetimes
and the other OAuth settings are in the
[configuration reference](/docs/self-hosting/configuration/#oauth-server-and-mcp).

## Limit or turn off MCP access

There's no setting that removes the `/mcp` endpoint, but it accepts only OAuth tokens that a user approved with the MCP
scope. To control access:

- **Allow only clients you create.** Set `EX_OAuthServer__DynamicClientRegistrationIpLimit` to `0` and
  `EX_OAuthServer__EnableClientIdMetadataDocuments` to `false`. Then only the OAuth apps a global administrator creates
  under **Settings** > **System** > **OAuth Apps** can connect.
- **Cut off one client.** Under **OAuth Apps**, open the client, check **Disabled**, and select **Save Changes**.
  Disabled clients can't authorize, refresh, or use existing access tokens.
- **Turn it off.** Block `/mcp` at your reverse proxy.

People can also disconnect apps from their own account. See [Your account](/docs/account/).

## Set it up with a coding agent

```prompt agent
Read https://exceptionless.com/docs/self-hosting/ai.md. Add the settings to my Exceptionless compose.yaml to turn on Exie with OpenRouter, reading the API key from an .env file, and allow MCP connections from https://agent.example.com.
```
