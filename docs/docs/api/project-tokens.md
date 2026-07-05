---
title: "Client-Scoped Tokens"
---

# Client-Scoped Tokens

You'll likely want your events associated with a specific project, and you may want to fetch data from a specific project. To ensure this, you need to generate a project-specific API key (or token). You can do this in the Exceptionless interface by clicking the All Projects link in the navigation header, then hover over the project name and click the gear icon. On the Settings page, you'll see a tab for API Keys. You can generate a token there.

However, you can programmatically generate these tokens as well. Let's use the [User Scoped Token](/docs/api/api-getting-started) you generated previously to get a list of projects.

### Get Projects

GET `api/v2/projects`

```shell
curl --location --request GET "https://api.exceptionless.com/api/v2/projects" \
--header 'Authorization: Bearer YOUR_USER_SCOPED_TOKEN'
```

The response to this request will be an array of all of your projects that looks like this:

```json
[
    {
        "id": "YOUR PROJECT ID",
        "created_utc": "2016-01-11T20:05:59.7185672",
        "organization_id": "YOUR ORG ID",
        "organization_name": "YOUR ORG NAME",
        "name": "YOUR PROJECT NAME",
        "delete_bot_data_enabled": false,
        "is_configured": true,
        "stack_count": 0,
        "event_count": 0,
        "has_premium_features": true,
        "has_slack_integration": false
    }
]
```

You'll need the `id` field from this response to generate your new project-specific token. Let's generate that now. In addition to using the project ID, we will also need to pass in scopes for the token. In this case, we are going to pass in the `client` scope which provides access to post events and read events, but doesn't provide full user-token access. [Read more about scopes here](/docs/api/api-getting-started).

### Generate Client-Scoped Token

POST `api/v2/projects/PROJECT_ID`

```shell
curl --location --request POST "https://api.exceptionless.com/api/v2/projects/YOUR_PROJECT_ID/tokens" \
--header 'Authorization: Bearer YOUR_USER_SCOPED_TOKEN' \
--header 'Content-Type: application/json' \
--data-raw '{
    "scopes": [
        "client"
    ]
}'
```

The response you'll receive will look like this:

```json
{
    "id": "TOKEN",
    "organization_id": "YOUR_ORG_ID",
    "project_id": "YOUR_PROJECT_ID",
    "scopes": [
        "client"
    ],
    "is_disabled": false,
    "created_utc": "2020-11-05T14:02:54.1866886Z",
    "updated_utc": "2020-11-05T14:02:54.1867055Z"
}
```

This `TOKEN` can now be used as your API key in Bearer authorization headers for subsequent API requests related to your project.

---

[Next > Posting Events](/docs/api/posting-events)
