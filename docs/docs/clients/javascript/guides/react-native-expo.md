---
title: "React Native and Expo"
order: 0
---

# React Native and Expo

The `@exceptionless/react-native` package reports JavaScript errors and events from React Native and Expo applications.
It also captures native iOS crashes in development and standalone builds.

## Install

For an Expo application:

```bash
npx expo install @exceptionless/react-native @react-native-async-storage/async-storage
```

For a bare React Native application:

```bash
npm install @exceptionless/react-native @react-native-async-storage/async-storage
cd ios && pod install
```

## Configure Expo native crash reporting

Add the Exceptionless config plugin to `app.json`:

```json
{
  "expo": {
    "plugins": ["@exceptionless/react-native/expo-plugin"]
  }
}
```

Native iOS crash reporting requires an Expo development build or standalone build. It is unavailable in Expo Go because
Expo Go cannot load custom native modules. JavaScript errors and events work in Expo Go.

## Start the client

Call `startup` once while the application initializes:

```tsx
import { Exceptionless } from "@exceptionless/react-native";

await Exceptionless.startup((configuration) => {
  configuration.apiKey = "API_KEY_HERE";
  configuration.defaultTags.push("react-native");
});
```

For a self-hosted Exceptionless instance, also set `configuration.serverUrl` to the root URL of your server.

## Add a React error boundary

Wrap the application tree to report React rendering errors:

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

## Send an event

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

## Support boundaries

- Unhandled JavaScript errors and promise rejections are captured on iOS and Android.
- Native iOS exceptions, signals, and Mach exceptions are submitted on the next launch.
- Native Android crash reporting is not currently implemented; JavaScript reporting still works on Android.
- Persistent event queue storage uses `@react-native-async-storage/async-storage`.

Use the maintained
[Expo example application](https://github.com/exceptionless/Exceptionless.JavaScript/tree/master/example/expo) to
exercise caught errors, unhandled errors, promise rejections, logs, feature usage, sessions, identity, error boundaries,
and native iOS crash submission.
