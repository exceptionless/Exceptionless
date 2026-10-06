---
title: "API Usage"
---

# API Usage

Our [API](https://api.exceptionless.io) utilizes Swagger and Swashbuckle to automatically generate, update, and display documentation (which means it works without any work on your end when you are self-hosting). It is a great resource for our users that want to get their hands dirty and use Exceptionless data to roll their own tools, dashboards, etc.

To view the full API documentation, visit <https://api.exceptionless.io> and click on the `API Documentation` link to be taken to the API documentation. With Swagger, you will have examples for all endpoints that include required parameters and potential responses.

## MCP OAuth access

OAuth discovery advertises the scopes the server supports. Each OAuth application has its own allowed scopes, and each authorization grant contains only the scopes and organizations approved by the user. Discovery does not grant every advertised permission to an application.

For the MCP resource, `mcp:read` is required to connect. `projects:read`, `stacks:read`, and `events:read` permit the corresponding read tools. `stacks:write` permits changing stack status (open, fixed, ignored, or discarded), snoozing stacks, changing whether future occurrences are critical, and adding or removing reference links. Access remains limited to the organizations in the grant and the user's current membership.

`offline_access` permits refresh token issuance so the client can obtain another access token without a new interactive authorization. It adds no read or write permission. Access tokens default to one hour and refresh tokens to 30 days; these lifetimes are configured with `OAuthServer:AccessTokenLifetimeMinutes` and `OAuthServer:RefreshTokenLifetimeDays`.

Dynamic registration without a scope uses `mcp:read projects:read stacks:read events:read offline_access`. It excludes `stacks:write`. An explicit registration scope is retained, so a client registered with only the four read scopes also lacks `offline_access`.

### Resolving a scope error after registration

If authorization reports scopes that are not allowed for the application, restart the client's authorization flow requesting an allowed subset. Keep `mcp:read` for MCP access. Changing checkboxes on a failed consent page does not validate a new request.

Client software should register the scopes it will request. Each grant remains limited to the scopes and organizations approved by the user. Account **Applications** lists and revokes the user's grants.

Existing access and refresh tokens do not gain additional permissions automatically. Fresh consent is required for additional access, including refresh tokens when the original grant did not include `offline_access`.

---

[Next > Getting Started](/docs/api/api-getting-started)
