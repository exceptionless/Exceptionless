---
title: "Posting Events"
---

# Posting Events

This is the meat of what Exceptionless does. This is what you care about. So, let's explore some of the possibilities.

Events passed through to Exceptionless take three forms:

- [Posting Messages](#posting-messages)
- [Posting Logs](#posting-logs)
- [Posting Errors](#posting-errors)

We'll explore how to send through events for each category. First, it's important to remind you that you should not being user-scoped tokens for these API endpoints. If you have not yet generated a client-scoped token, [do so through the UI](/docs/project-settings) or [follow the guide here to do so programmatically here](/docs/api/project-tokens).

### Posting Messages

Messages are arbitrary pieces of information that can mean or relate to anything. They don't have to be errors or logs. Configuring a message event is simple.

POST `api/v2/events`

```shell
curl --location --request POST "https://api.exceptionless.com/api/v2/events" \
--header 'Authorization: Bearer YOUR_PROJECT_TOKEN' \
--header 'Content-Type: application/json' \
--data-raw '{ "message": "Exceptionless is amazing!" }'
```

You will receive a `202` response if the message was successfully posted. You can check out your Exceptionless dashboard to immediately see this message show up.

### Posting Logs

Logs will generally have a little more information associated with them than messages. Logs can take fields like date, message, and name. Let's take a look at an example.

POST `api/v2/events`

```shell
curl --location --request POST "https://api.exceptionless.com/api/v2/events" \
--header 'Authorization: Bearer YOUR_PROJECT_TOKEN' \
--header 'Content-Type: application/json' \
--data-raw '{ "type": "log", "message": "Exceptionless is amazing!", "date":"2030-01-01T12:00:00.0000000-05:00", "@user":{ "identity":"123456789", "name": "Test User" } }'
```

You will receive a `202` response if the log was successfully posted. You can check out your Exceptionless dashboard to immediately see this log show up.

### Posting Errors

Errors will generally be the most comprehensive events you send through to Exceptionless. They contain many details about the problems your users are facing. Let's take a look at the fields you'll need to provide and how to submit errors.

POST `api/v2/events`

```shell
curl --location --request POST "https://api.exceptionless.com/api/v2/events" \
--header 'Authorization: Bearer YOUR_PROJECT_TOKEN' \
--header 'Content-Type: application/json' \
--data-raw '{ "type": "error", "date":"2030-01-01T12:00:00.0000000-05:00", "@simple_error": { "message": "Simple Exception", "type": "System.Exception", "stack_trace": " at Client.Tests.ExceptionlessClientTests.CanSubmitSimpleException() in ExceptionlessClientTests.cs:line 77" } }'
```

With an error event, you will be able to explore additional details for your event through Exceptionless's dashboard. These additional details include the full stack trace if you provided it.

Next up, we'll take a look at how we fetch events.

---

[Next > Getting Events](/docs/api/getting-events)
