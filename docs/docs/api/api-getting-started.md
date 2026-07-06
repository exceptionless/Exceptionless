---
title: "Getting Started"
---

# Getting Started

Remember, you can refer to the full, self-updating, documentation [here](https://api.exceptionless.io). But let's get started with some real examples to get you authenticated, which will allow access to other endpoints. Exceptionless protects your account by requiring authentication which takes the form of a `Bearer` Authorization header.

Before we generate a user-scoped token, let's talk a little bit about scopes.

## Authentication Scopes

When you eventually create an API Key/token for your project, you will need to pass in a scope (if you are creating this token programmatically). Exceptionless recognizes two scopes:

* user
* client

The `user` scope has full admin access to the account. This scope creates a token that can do everything from create projects to update billing info.

The `client` scope has access to post events, getting events, and reading the client configuration for a project.

## Get Your User Scoped Token

Before you can post project-specific events and make project-specific API requests, you'll need to first generate a user token which can then be used to generate tokens for your projects. It is incredibly important to protect user-scoped tokens as they act as the keys to the kingdom. **Never let anyone else access your user token**.

*NOTE: If you signed up using an OAuth flow with Google or something else, you will need to create a local login to be able to use this endpoint.*

Let's take a look at an example.

POST `/api/v2/auth/login`

```shell
curl --location --request POST "https://api.exceptionless.com/api/v2/auth/login" \
--header 'Content-Type: application/json' \
--data-raw '{
    "email": YOUR_EMAIL,
    "password": PASSWORD
}'
```

Your response should look like this:

```json
{
    "token": "ojcQ1YVtKBnFITzJB3RFkdWRaVGdghHZoHvGKbx4"
}
```

Now that you have your token, you can get your project-specific API key (or token) which will allow you to execute API requests against a specific project. It's worth also noting that you can easily update your requests to authenticate via a URL query string and your access token.

For example, if we want to view our organizations, we simply navigate to <https://api.exceptionless.io/api/v2/organizations>, add the query string `?access_token={token}` rather than a Bearer token authorization header.

---

[Next > Getting Project Tokens](/docs/api/project-tokens)
