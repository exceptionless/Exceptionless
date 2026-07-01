---
title: "Express"
order: 4
---

# Express

Perhaps the most popular NodeJS server-side framework, Express is used in thousands of projects. Exceptionless provides dedicated NodeJS support, and configuring your Exceptionless client in Express is easy.

### Install

To install exceptionless, you can use npm or yarn:

npm - `npm install @exceptionless/node`

yarn - `yarn add @exceptionless/node`

### Initializing the Client

Exceptionless provides a default singleton client instance. While we recommend
using the default client instance for most use cases, you can also create
custom instances (though that's beyond the scope of this guide).

```javascript
import { Exceptionless } from "@exceptionless/node";

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR API KEY";
});
```

You can see an additional parameter passed into the configuration object as an
example. To see all the available options, take a look at our
[configuration values here](/docs/clients/javascript/client-configuration-values).

### Using Exceptionless in a Express App

In this example, we're just making use of the Exceptionless client in a file
that handles one of our API routes. We will also show off how to handle errors
and 404s in Express.

```js
import { Exceptionless, KnownEventDataKeys } from "@exceptionless/node";
import express from "express";

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR API KEY";
});

const app = express();
app.get("/", async (req, res) => {
  await Exceptionless.submitLog("Hello World!");
  res.send("Hello World!");
});

app.use(async (err, req, res, next) => {
  if (res.headersSent) {
    return next(err);
  }

  await Exceptionless.createUnhandledException(err, "express").setContextProperty(KnownEventDataKeys.RequestInfo, req).submit();
  res.status(500).send("Something broke!");
});

app.use(async (req, res) => {
  await Exceptionless.createNotFound(req.originalUrl).setContextProperty(KnownEventDataKeys.RequestInfo, req).submit();
  res.status(404).send("Sorry cant find that!");
});

const server = app.listen(3000, async () => {
  var host = server.address().address;
  var port = server.address().port;

  var message = "Example app listening at http://" + host + port;
  await Exceptionless.submitLog("app", message, "Info");
});
```

With that set up, you can use the Exceptionless client anywhere in your app.
