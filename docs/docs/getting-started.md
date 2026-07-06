---
title: "Getting Started"
---

# Getting Started

Exceptionless provides you the tools to track errors, logs, and events while guiding you toward actionable solutions. To get started, you'll want to decide if you are self-hosting Exceptionless or using our hosted version. If you choose to use our hosted version, you can get started for free.

## Hosted Option

1. [Create an account](https://be.exceptionless.io/signup)
2. When you sign-up, you will be prompted to create your first project.
3. [Configure your application](https://be.exceptionless.io/project/list) by clicking the Download & Configure Client action button on the project list page.
4. Select your project type and follow the instructions.
5. Your application will now automatically send all unhandled errors to the Exceptionless service.
6. You can also send handled errors, feature usage or log messages along with additional information ([see documentation for your specific client](/docs/clients/)).

## Self-Hosted Option

We have put together comprehensive documentation to help you get started with a self-hosted Exceptionless instance. You can [find that documentation here](/docs/self-hosting/).

## Sending Your First Event

Once you've singed up for an account and created a project, you can start receiving events. Let's take a look at sending a simple event to Exceptionless.

POST `api/v2/events`

```shell
curl --location --request POST "https://api.exceptionless.com/api/v2/events" \
--header 'Authorization: Bearer YOUR_PROJECT_TOKEN' \
--header 'Content-Type: application/json' \
--data-raw '{ "type": "error", "date":"2030-01-01T12:00:00.0000000-05:00", "@simple_error": { "message": "Simple Exception", "type": "System.Exception", "stack_trace": " at Client.Tests.ExceptionlessClientTests.CanSubmitSimpleException() in ExceptionlessClientTests.cs:line 77" } }'
```

---

You've got your account created, now what? Let's get a better understanding of how you manage events in Exceptionless and then we can dive into some best practice and ways to enhance your use.

---

[Next > Managing Stacks](/docs/managing-stacks)
