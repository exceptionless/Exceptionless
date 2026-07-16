---
title: "Express Example"
---

# Express Example

Add Exceptionless to your Express.js project and start collecting unhandled errors and 404s quickly.

To start, just add the following middleware to the bottom of your middleware definitions.

```js
import { Exceptionless } from "@exceptionless/node";

// This middleware processes any unhandled errors that may occur in your middleware.
app.use(async (err, req, res, next) => {
  if (res.headersSent) {
    return next(err)
  }

  await Exceptionless.createUnhandledException(err, "express")
    .addRequestInfo(req)
    .submit();

  res.status(500).send("Something broke!");
});

// This middleware processes 404’s.
app.use(async (req, res) => {
  await Exceptionless.createNotFound(req.originalUrl).addRequestInfo(req).submit();
  res.status(404).send("Sorry cant find that!");
});
```

## Sample Express.js App

We have built a quick [Express.js sample app](https://github.com/exceptionless/Exceptionless.JavaScript/blob/master/example/express/app.js) you can play around with.

**Run the sample app by following the steps below:**

1. Install [Node.js](https://nodejs.org/)
2. [Clone or download our repository from GitHub](https://github.com/exceptionless/Exceptionless.JavaScript).
3. Run `npm install`. This steps is required because we reference the exceptionless package from the root dist folder.
4. Navigate to the `example\express` folder via the command line (e.g., cd example\express)
5. Open app.js in your favorite text editor and set the [`apiKey`](https://github.com/exceptionless/Exceptionless.JavaScript/blob/master/example/express/app.js#L7). You may need to remove the `serverUrl` setting if you are not self hosting.
6. Run node app.js.
7. Navigate to <http://localhost:3000> in your browser to view the express app.
8. To create an error, navigate to <http://localhost:3000/boom>

### Troubleshooting

We recommend enabling debug logging by calling `Exceptionless.config.useDebugLogger();`. This will output messages to the console regarding what the client is doing. Please contact us by creating an issue on GitHub if you need assistance or have any feedback for the project.

---

[Next > Angular Example](/docs/clients/javascript/react-example)
