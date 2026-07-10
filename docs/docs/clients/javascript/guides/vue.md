---
title: "Vue"
order: 2
---

# Vue

Vue is one of the more popular JavaScript frameworks out there, and Exceptionless has your back if you're working with it. Getting started is simple.

### Install

To install exceptionless, you can use npm or yarn:

npm - `npm install @exceptionless/vue`

yarn - `yarn add @exceptionless/vue`

### Initializing the Client

Exceptionless provides a default singleton client instance. While we recommend
using the default client instance for most use cases, you can also create
custom instances (though that's beyond the scope of this guide).

```javascript
import { Exceptionless } from "@exceptionless/vue";

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR API KEY";
});
```

You can see an additional parameter passed into the configuration object as an
example. To see all the available options, take a look at our
[configuration values here](/docs/clients/javascript/client-configuration-values).

### Using Exceptionless in a Vue App

```javascript
import { createApp } from "vue";
import App from "./App.vue";
import { Exceptionless, ExceptionlessErrorHandler } from "@exceptionless/vue";

Exceptionless.startup((c) => {
  c.apiKey = "YOUR API KEY";
});

const app = createApp(App);
// Set the global vue error handler.
app.config.errorHandler = ExceptionlessErrorHandler;
app.mount("#app");
```

With that set up, you can use the Exceptionless client anywhere in your app.

---

[Next > Angular](/docs/clients/javascript/guides/angular)
