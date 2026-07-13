---
title: "Supported Platforms"
order: 0
---

# JavaScript and TypeScript Supported Platforms

Exceptionless.JavaScript uses focused packages so each application gets the runtime integrations it needs without
carrying unrelated code.

## Runtime support

| Runtime             | Package                       | Automatic capture                                                                                | Notes                                                                                                                      |
| ------------------- | ----------------------------- | ------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------- |
| Browser             | `@exceptionless/browser`      | Unhandled errors and browser context                                                             | Install from npm or load the ESM bundle from a CDN.                                                                        |
| Node.js 18 or newer | `@exceptionless/node`         | Uncaught exceptions, unhandled rejections, process lifecycle, and Node context                   | Use `--enable-source-maps` for improved TypeScript stack traces.                                                           |
| Deno                | `@exceptionless/core`         | Manual submission                                                                                | Uses Deno's npm compatibility and built-in `fetch`. Add Deno-specific unhandled-error hooks in your application if needed. |
| React Native        | `@exceptionless/react-native` | Unhandled JavaScript errors, promise rejections, and native iOS crashes                          | JavaScript reporting works on iOS and Android. Native crash capture is currently iOS-only.                                 |
| Expo                | `@exceptionless/react-native` | Same JavaScript capture as React Native; native iOS crashes in development and standalone builds | Expo Go cannot load the native crash reporter, but JavaScript reporting still works.                                       |

## Framework packages and examples

| Framework | Package or example                                                                                                                                     |
| --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| React     | [`@exceptionless/react`](https://www.npmjs.com/package/@exceptionless/react) and the [React guide](/docs/clients/javascript/guides/react/)             |
| Vue       | [`@exceptionless/vue`](https://www.npmjs.com/package/@exceptionless/vue) and the [Vue guide](/docs/clients/javascript/guides/vue/)                     |
| AngularJS | [`@exceptionless/angularjs`](https://www.npmjs.com/package/@exceptionless/angularjs) and the [Angular guide](/docs/clients/javascript/guides/angular/) |
| Express   | [`@exceptionless/node`](https://www.npmjs.com/package/@exceptionless/node) and the [Express guide](/docs/clients/javascript/guides/express/)           |
| Next.js   | [Next.js example](https://github.com/exceptionless/Exceptionless.JavaScript/tree/master/example/nextjs)                                                |
| SvelteKit | [SvelteKit example](https://github.com/exceptionless/Exceptionless.JavaScript/tree/master/example/svelte-kit)                                          |
| Expo      | [Expo example](https://github.com/exceptionless/Exceptionless.JavaScript/tree/master/example/expo)                                                     |

All JavaScript packages are developed in the
[Exceptionless.JavaScript repository](https://github.com/exceptionless/Exceptionless.JavaScript). If your environment is
not listed, start with the runtime-neutral `@exceptionless/core` package or use the
[Exceptionless HTTP API](/docs/clients/custom-clients/).

---

[Next > Configuration](/docs/clients/javascript/client-configuration)
