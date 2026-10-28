---
title: "JavaScript framework guides"
navTitle: "Framework guides"
description: "Set up Exceptionless in a browser app, Node.js, Express, React, React Native and Expo, Next.js, SvelteKit, Vue, or Angular."
order: 1
redirectFrom:
  - /docs/clients/javascript/guides/more-guides/
sources:
  - Exceptionless.JavaScript:example/
  - Exceptionless.JavaScript:packages/
---

# JavaScript framework guides

Each guide installs the right package, starts the client, wires it into the framework's error hooks, and checks that a
test error arrives.

| Guide                                                                  | Package                                            | Covers                                                                |
| ---------------------------------------------------------------------- | -------------------------------------------------- | --------------------------------------------------------------------- |
| [Browser](/docs/clients/javascript/guides/browser/)                    | `@exceptionless/browser`                           | npm and CDN setup for any web page.                                   |
| [Node.js and serverless](/docs/clients/javascript/guides/node/)        | `@exceptionless/node`                              | Servers, scripts, workers, and functions that must flush before exit. |
| [Express](/docs/clients/javascript/guides/express/)                    | `@exceptionless/node`                              | Error and 404 middleware with request details.                        |
| [React](/docs/clients/javascript/guides/react/)                        | `@exceptionless/react`                             | `ExceptionlessErrorBoundary` with a fallback UI.                      |
| [React Native and Expo](/docs/clients/javascript/guides/react-native/) | `@exceptionless/react-native`                      | JavaScript errors, the Expo config plugin, and native iOS crashes.    |
| [Next.js](/docs/clients/javascript/guides/nextjs/)                     | `@exceptionless/browser` and `@exceptionless/node` | App Router client and server errors.                                  |
| [SvelteKit](/docs/clients/javascript/guides/sveltekit/)                | `@exceptionless/browser` and `@exceptionless/node` | Client and server `handleError` hooks.                                |
| [Vue](/docs/clients/javascript/guides/vue/)                            | `@exceptionless/vue`                               | The global Vue error handler.                                         |
| [Angular](/docs/clients/javascript/guides/angular/)                    | `@exceptionless/browser`                           | An `ErrorHandler` provider for standalone apps.                       |

For AngularJS 1.x, see the
[`@exceptionless/angularjs` README](https://github.com/exceptionless/Exceptionless.JavaScript/tree/main/packages/angularjs).

## Example apps

The [Exceptionless.JavaScript repository](https://github.com/exceptionless/Exceptionless.JavaScript/tree/main/example)
has a runnable app for the browser, Express, React, Vue, Next.js, SvelteKit, and Expo. To run one:

1. Install [Node.js](https://nodejs.org/).
2. Clone the repository and run `npm install` and then `npm run build` from its root. The examples use the packages
   built from the repository.
3. Point the example at your project. The examples send to a local development server, so replace the `apiKey` value
   with your project's API key and remove or change the `serverUrl`. The Next.js example reads both from environment
   variables instead (see its README).
4. Start the example from its folder, for example `cd example/react` and then `npm start`. Each example's `package.json`
   lists its scripts.
