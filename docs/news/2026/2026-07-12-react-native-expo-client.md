---
title: "Exceptionless for React Native and Expo"
date: 2026-07-12
---

# Exceptionless for React Native and Expo

Mobile applications fail in more than one place. JavaScript can throw during rendering or an asynchronous operation,
the native process can crash below the JavaScript runtime, and a device can go offline before an error report reaches
your server. A useful mobile client has to account for all three.

Exceptionless.JavaScript now includes first-class React Native and Expo support through the
[`@exceptionless/react-native` package](https://www.npmjs.com/package/@exceptionless/react-native). The client combines
automatic JavaScript error capture, persistent delivery, React Native context, and native iOS crash reporting with the
same Exceptionless event APIs used in browser and Node.js applications.

The work shipped in
[`Exceptionless.JavaScript` pull request #155](https://github.com/exceptionless/Exceptionless.JavaScript/pull/155) and is
available on npm today.

## One client for errors, logs, and product signals

The React Native client does more than forward an exception message. It captures and enriches the information needed to
understand what failed, where it failed, and which application session was affected.

The client includes:

- Automatic capture of unhandled JavaScript errors and promise rejections
- React Native and Hermes stack parsing that preserves structured application frames
- React error boundary support, including the React component stack
- Persistent event queueing through AsyncStorage
- Application lifecycle and session tracking
- Device, operating system, locale, and React Native environment data when available
- The standard Exceptionless APIs for exceptions, logs, feature usage, custom events, user identity, tags, and custom
  data

Hermes and Metro stack traces need special handling because their frames do not always resemble browser or Node.js
stacks. The new parser identifies the real application frame, retains native frames, and avoids using synthetic Hermes
wrappers as the error signature when a better application method is available. That produces more useful stack traces
and more stable grouping in Exceptionless.

## Native iOS crash reporting

JavaScript error handlers cannot observe every mobile failure. An Objective-C or Swift exception, segmentation fault,
or other fatal native signal can terminate the process before JavaScript has a chance to respond.

On iOS, `@exceptionless/react-native` integrates
[PLCrashReporter](https://github.com/microsoft/plcrashreporter) through a native module. It captures Objective-C and Swift
exceptions, signals such as `SIGSEGV` and `SIGABRT`, and Mach exceptions such as `EXC_BAD_ACCESS`. The report is written
to the device and submitted to Exceptionless the next time the application starts.

Native crash capture is currently available on iOS. Android applications receive the full JavaScript reporting path,
including unhandled errors, promise rejections, event queueing, and environment data, but native Android crash capture
is not yet implemented.

## Designed for Expo workflows

Expo applications can install the client and its AsyncStorage dependency with the Expo package manager:

```bash
npx expo install @exceptionless/react-native @react-native-async-storage/async-storage
```

Call `startup` once while the application initializes:

```tsx
import { Exceptionless } from "@exceptionless/react-native";

await Exceptionless.startup((configuration) => {
  configuration.apiKey = "API_KEY_HERE";
  configuration.defaultTags.push("react-native");
});
```

For native iOS crash reporting, add the included config plugin to `app.json`:

```json
{
  "expo": {
    "plugins": ["@exceptionless/react-native/expo-plugin"]
  }
}
```

JavaScript errors and events work in Expo Go. Native iOS crash reporting requires an Expo development build or
standalone build because Expo Go cannot load custom native modules. Bare React Native applications can install the same
package and use CocoaPods for iOS native linking.

## Report rendering errors with an error boundary

The package includes an Exceptionless-aware React error boundary. It reports the JavaScript error and React component
stack before rendering the fallback interface:

```tsx
import { ExceptionlessErrorBoundary } from "@exceptionless/react-native";

export function App() {
  return (
    <ExceptionlessErrorBoundary fallback={<Text>Something went wrong.</Text>}>
      <RootNavigator />
    </ExceptionlessErrorBoundary>
  );
}
```

Applications can continue to submit events directly through the familiar Exceptionless API:

```tsx
import { Exceptionless, toError } from "@exceptionless/react-native";

try {
  await saveOrder();
} catch (error) {
  await Exceptionless.submitException(toError(error));
}

await Exceptionless.submitLog("Application started");
await Exceptionless.submitFeatureUsage("Checkout");
```

Self-hosted users can set `configuration.serverUrl` during startup to send events to their own Exceptionless instance.
The existing client configuration options for tags, default data, user identity, data exclusions, and sessions work with
the React Native package, too.

## Tested against real application paths

The implementation was validated across the package, native integration, and example application rather than only at
the TypeScript boundary. The merged pull request completed:

- 342 automated tests
- Builds across every package and example in the JavaScript workspace
- ESLint and Prettier validation
- A dry-run package build to verify the published npm contents
- CocoaPods installation for the Expo iOS project
- An iPad simulator build and launch
- Manual Expo testing of error boundaries, reset behavior, event queueing, and delivery to a local Exceptionless server

The review also hardened the Hermes stack parser against inefficient regular expressions, corrected error boundary reset
behavior, protected pending native crash reports, and aligned iOS lifecycle handling so transient inactive states do not
end sessions twice.

## Try the example application

The maintained [Expo example application](https://github.com/exceptionless/Exceptionless.JavaScript/tree/master/example/expo)
demonstrates caught and unhandled errors, promise rejections, logs, feature usage, sessions, user identity, React error
boundaries, and native iOS crash submission. It is both a starting point for integration and the dogfood application we
use to exercise the client against a running Exceptionless server.

Follow the [React Native guide](/docs/clients/javascript/guides/react-native-expo/) for installation, Expo configuration,
application startup, error boundaries, self-hosting, and current platform boundaries. The expanded
[client directory](/docs/clients/) and [platform support matrix](/docs/clients/javascript/supported-platforms/) also show
how React Native and Expo fit alongside our Browser, Node.js, Deno, and framework-specific clients.

If you find an issue or need support for another mobile workflow, open an
[SDK issue](https://github.com/exceptionless/Exceptionless.JavaScript/issues) or join the conversation in the
[implementation pull request](https://github.com/exceptionless/Exceptionless.JavaScript/pull/155).
