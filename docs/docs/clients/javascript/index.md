---
title: "JavaScript Client"
---

# JavaScript Client

The Exceptionless JavaScript client SDK makes it easy to report errors, log details, track feature usage and more. Be sure you have an Exceptionless account ([you can sign up here](/)) or that you are self-hosting a running instance of Exceptionless.

---

Full guides can be found below:

* [React](/docs/clients/javascript/guides/react)
* [Vue](/docs/clients/javascript/guides/vue)
* [Angular](/docs/clients/javascript/guides/angular)
* [Node](/docs/clients/javascript/node-example)
* [Express](/docs/clients/javascript/guides/express)

---

This quickstart focuses on the vanilla JavaScript implementation of Exceptionless.

## npm

To install with npm, run: `npm install @exceptionless/browser`

Next, you just need to call startup during your apps startup to automatically capture unhandled errors.

```js
import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.startup("API KEY HERE");

try {
  throw new Error("test");
} catch (error) {
  await Exceptionless.submitException(error);
}
```

## CDN

To install via a script tag referencing Exceptionless over a CDN, add the following before your closing `<body>` tag and call startup like so:

```html
`<script type="module">`
  import { Exceptionless } from "https://unpkg.com/@exceptionless/browser";
  await Exceptionless.startup("API KEY HERE");

  try {
    throw new Error("test");
  } catch (error) {
    await Exceptionless.submitException(error);
  }
`</script>`
```

---

[Next > Configuration](/docs/clients/javascript/client-configuration)
