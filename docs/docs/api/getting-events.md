---
title: "Getting Events"
---

# Getting Events

There may be times where you need to access the events you've sent through to Exceptionless without going through the Exceptionless UI. For those situations, you can use the API to fetch events. You can specify events across your organization or specific to a project. Let's take a look at the options.

*Note: organization-level requests require your [Scoped User Token](/docs/api/api-getting-started) while projects-specific requests can use your [Project Token](/docs/api/project-tokens).*

### Get Count of All Events

GET `api/v2/events/count`

```shell
curl --location --request GET "https://api.exceptionless.com/api/v2/events/count" \
--header 'Authorization: Bearer YOUR_SCOPED_USER_TOKEN'
```

The response will look like this:

```json
{
    "total": 74
}
```

### Get Count of All Events For a Single Project

Remember, you can get your Project ID in the UI when logged in or by [following the instructions here](/docs/api/project-tokens#get-projects).

GET `api/v2/projects/YOUR_PROJECT_ID/events/count`

```shell
curl --location --request GET "https://api.exceptionless.com/api/v2/projects/YOUR_PROJECT_ID/events/count" \
--header 'Authorization: Bearer YOUR_PROJECT_TOKEN'
```

The response will be just like the global event search:

```json
{
    "total": 74
}
```

### Get All Events For Organization

GET `api/v2/events`

```shell
curl --location --request GET 'https://api.exceptionless.com/api/v2/events' \
--header 'Authorization: Bearer YOUR_SCOPED_USER_TOKEN'
```

The default results returned will be paginated and limited to 10 results per page. However, you can configure the results limit and a bunch of other properties for your search through query string parameters. The full options [are listed here](https://api.exceptionless.io/docs/index.html).

Your response will look like this:

```json
[
  {
    "type": "string",
    "source": "string",
    "date": "2020-11-06T13:57:46.773Z",
    "tags": [
      "string"
    ],
    "message": "string",
    "geo": "string",
    "value": 0,
    "count": 0,
    "data": {},
    "referenceId": "string",
    "id": "string",
    "organizationId": "string",
    "projectId": "string",
    "stackId": "string",
    "isFirstOccurrence": true,
    "createdUtc": "2020-11-06T13:57:46.773Z",
    "idx": {}
  }
]
```

### Get All Events For a Project

Similar to getting all events or your organization, you can get all events for a project. You have the same query string filtering capabilities with this request, but we'll keep the example simple.

GET `api/v2/projects/YOUR_PROJECT_ID/events`

```shell
curl --location --request GET 'https://api.exceptionless.com/api/v2/projects/YOUR_PROJECT_ID/events' \
--header 'Authorization: Bearer YOUR_PROJECT_TOKEN'
```

Again, the response will look like this:

```json
[
  {
    "type": "string",
    "source": "string",
    "date": "2020-11-06T13:57:46.773Z",
    "tags": [
      "string"
    ],
    "message": "string",
    "geo": "string",
    "value": 0,
    "count": 0,
    "data": {},
    "referenceId": "string",
    "id": "string",
    "organizationId": "string",
    "projectId": "string",
    "stackId": "string",
    "isFirstOccurrence": true,
    "createdUtc": "2020-11-06T13:57:46.773Z",
    "idx": {}
  }
]
```

### Getting an Event by ID

You can get the details of a single event by passing in the ID for the event.

GET `api/v2/events/YOUR_EVENT_ID`

```shell
curl --location --request GET 'https://api.exceptionless.com/api/v2/events/YOUR_EVENT_ID' \
--header 'Authorization: Bearer YOUR_PROJECT_TOKEN'
```

The response will be a single object with the details of the event like this:

```json
{
  "type": "string",
  "source": "string",
  "date": "2020-11-06T14:02:45.101Z",
  "tags": [
    "string"
  ],
  "message": "string",
  "geo": "string",
  "value": 0,
  "count": 0,
  "data": {},
  "referenceId": "string",
  "id": "string",
  "organizationId": "string",
  "projectId": "string",
  "stackId": "string",
  "isFirstOccurrence": true,
  "createdUtc": "2020-11-06T14:02:45.101Z",
  "idx": {}
}
```

---

[Next > Clients](/docs/clients/)
