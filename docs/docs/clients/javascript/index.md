---
title: "JavaScript Client"
---

# JavaScript Client

The Exceptionless JavaScript and TypeScript SDK makes it easy to report errors, submit logs, track feature usage, and capture runtime-specific context. It supports Browser, Node.js, Deno, React Native, and Expo applications, with packages and examples for popular frameworks.

## Choose your environment

| Environment | Package | Guide |
| --- | --- | --- |
| Browser and browser frameworks | `@exceptionless/browser` | [Browser quickstart below](#browser-quickstart) |
| Node.js | `@exceptionless/node` | [Node.js example](/docs/clients/javascript/node-example/) |
| Deno | `@exceptionless/core` | [Deno guide](/docs/clients/javascript/guides/deno/) |
| React Native and Expo | `@exceptionless/react-native` | [React Native and Expo guide](/docs/clients/javascript/guides/react-native-expo/) |
| React | `@exceptionless/react` | [React guide](/docs/clients/javascript/guides/react/) |
| Vue | `@exceptionless/vue` | [Vue guide](/docs/clients/javascript/guides/vue/) |
| AngularJS | `@exceptionless/angularjs` | [Angular guide](/docs/clients/javascript/guides/angular/) |

See the [supported platforms matrix](/docs/clients/javascript/supported-platforms/) for automatic-capture behavior, runtime requirements, and example applications including Express, Next.js, SvelteKit, and Expo.

## Browser quickstart

You need an Exceptionless project API key from either [Exceptionless Cloud](/) or your self-hosted instance.

## npm

To install with npm, run: `npm install @exceptionless/browser`

Call `startup` during application startup to automatically capture unhandled browser errors.

```js
import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.startup("API KEY HERE");

try {
  throw new Error("test");
} catch (error) {
  await Exceptionless.submitException(error);
}
```

### CDN

To install via a script tag referencing Exceptionless over a CDN, add the following before your closing `<body>` tag and call startup like so:

```html
<script type="module">
  import { Exceptionless } from "https://unpkg.com/@exceptionless/browser";
  await Exceptionless.startup("API KEY HERE");

  try {
    throw new Error("test");
  } catch (error) {
    await Exceptionless.submitException(error);
  }
</script>
```

---

[Next > Configuration](/docs/clients/javascript/client-configuration)
