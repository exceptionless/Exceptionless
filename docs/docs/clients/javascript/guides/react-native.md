---
title: "React Native and Expo"
metaTitle: "Exceptionless error tracking for React Native and Expo"
description: "Report JavaScript errors and native iOS crashes from React Native and Expo apps with @exceptionless/react-native and its Expo config plugin."
order: 5
sources:
  - Exceptionless.JavaScript:packages/react-native/README.md
  - Exceptionless.JavaScript:packages/react-native/package.json
  - Exceptionless.JavaScript:packages/react-native/exceptionless-react-native.podspec
  - Exceptionless.JavaScript:packages/react-native/react-native.config.cjs
  - Exceptionless.JavaScript:packages/react-native/expo-plugin/withExceptionless.cjs
  - Exceptionless.JavaScript:packages/react-native/src/ReactNativeExceptionlessClient.ts
  - Exceptionless.JavaScript:packages/react-native/src/ExceptionlessErrorBoundary.tsx
  - Exceptionless.JavaScript:packages/react-native/src/plugins/ReactNativeGlobalHandlerPlugin.ts
  - Exceptionless.JavaScript:packages/react-native/src/plugins/ReactNativeLifeCyclePlugin.ts
  - Exceptionless.JavaScript:packages/react-native/src/plugins/ReactNativeEnvironmentInfoPlugin.ts
  - Exceptionless.JavaScript:packages/react-native/src/plugins/NativeCrashPlugin.ts
  - Exceptionless.JavaScript:packages/react-native/ios/ExceptionlessCrashReporter.mm
  - Exceptionless.JavaScript:example/expo/README.md
  - Exceptionless.JavaScript:example/expo/App.tsx
  - Exceptionless.JavaScript:example/expo/app.json
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.cs
---

# React Native and Expo

`@exceptionless/react-native` reports unhandled JavaScript errors and promise rejections from React Native and Expo
apps, and on iOS it also reports native crashes: Objective-C and Swift exceptions, signals such as `SIGSEGV` and
`SIGABRT`, and Mach exceptions. Events wait in a queue stored with AsyncStorage, so they survive an app restart.

Requirements:

- React 18 or later and React Native 0.71 or later.
- `@react-native-async-storage/async-storage` 1.19 or later.
- For native crash reporting: iOS 14 or later, and a development build, standalone build, or React Native CLI app. Expo
  Go reports JavaScript errors only.

Android reports JavaScript errors and promise rejections. Native crash reporting on Android is not available yet.

Use this package instead of `@exceptionless/browser` or `@exceptionless/react` in React Native apps.

## Install

### Expo

```bash
npx expo install @exceptionless/react-native @react-native-async-storage/async-storage
```

Add the package's config plugin to `app.json`:

```json
{
  "expo": {
    "plugins": ["@exceptionless/react-native/expo-plugin"]
  }
}
```

Then rebuild the native app, for example with `npx expo run:ios` after installing `expo-dev-client`, or with an EAS
build. The native crash reporter is linked into the build automatically. Expo Go can't load the native module, so it
reports JavaScript errors but not native crashes.

### React Native CLI

```bash
npm install @exceptionless/react-native @react-native-async-storage/async-storage
cd ios && pod install
```

CocoaPods links [PLCrashReporter](https://github.com/microsoft/plcrashreporter), which the package uses for native crash
reporting.

## Configure

Start the client once, when the app's JavaScript loads, and wrap your app in `ExceptionlessErrorBoundary`:

```tsx
import { Text } from "react-native"
import { Exceptionless, ExceptionlessErrorBoundary } from "@exceptionless/react-native"

import HomeScreen from "./HomeScreen"

Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.version = "1.2.3" // optional: your app's version, used for regressions
})

export default function App() {
  return (
    <ExceptionlessErrorBoundary fallback={<Text>Something went wrong.</Text>}>
      <HomeScreen />
    </ExceptionlessErrorBoundary>
  )
}
```

`startup` returns a promise. It sets the API key right away, so you don't need to wait for it before rendering. On iOS
it also installs the native crash reporter and sends any crash reports saved by the previous launch.

The error boundary reports render errors with React's component stack and shows `fallback` in place of its children.
Errors in event handlers and async code are reported by the global handlers instead. Submit errors you catch yourself:

```tsx
import { Exceptionless, toError } from "@exceptionless/react-native"

export async function saveProfile(profile: unknown) {
  try {
    await fetch("https://example.com/api/profile", { method: "PUT", body: JSON.stringify(profile) })
  } catch (error) {
    await Exceptionless.submitException(toError(error))
  }
}
```

To group events into user sessions, identify the user and turn on sessions. Apps usually let the client create session
IDs:

```tsx
Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.setUserIdentity("user-123", "Jane Doe")
  c.useSessions(true, 60000, true)
})
```

See [Sessions](/docs/user-sessions/) for what the three arguments do.

> [!NOTE]
> Self-hosted instances use your own URL: add `c.serverUrl = "https://exceptionless.example.com"`. To reach a server on
> your development machine, use `http://localhost:<port>` from the iOS Simulator and `http://10.0.2.2:<port>` from the
> Android Emulator. Physical devices need your machine's LAN address.

## Verify

Add a temporary button that sends a handled error and flushes the queue:

```tsx
import { Button } from "react-native"
import { Exceptionless } from "@exceptionless/react-native"

export function SendTestError() {
  return (
    <Button
      title="Send a test error"
      onPress={async () => {
        await Exceptionless.submitException(new Error("Exceptionless test error"))
        await Exceptionless.processQueue()
      }}
    />
  )
}
```

Tap it, then open **Events** in Exceptionless. The event's **Environment** tab shows the OS, its version, and the React
Native version.

To check native crash reporting on iOS, run a development or release build without the Xcode debugger attached, crash
the app from native code, and open the app again. The crash is sent on the next launch as an unhandled error with a
`native_crash` property.

If nothing arrives, add `c.useDebugLogger()` to `startup` and watch the Metro logs. See
[Troubleshooting](/docs/clients/javascript/troubleshooting/).

## What is captured automatically

- **Unhandled JavaScript errors** through React Native's global error handler. The handler that was installed before,
  such as the red error screen in development, still runs.
- **Unhandled promise rejections.** Values that aren't an `Error` are converted to one.
- **Native iOS crashes**, saved by PLCrashReporter when the app crashes and sent on the next launch. Each one includes
  the crashed thread's frames, the signal or exception, and the device model, OS version, and app version.
- **Render errors** inside `ExceptionlessErrorBoundary`, with the component stack.
- **React Native and Hermes stack frames**, parsed into file, line, and column.
- **Device details**: the platform, OS version, React Native version, and locale, plus the device brand, manufacturer,
  and model on Android and the device idiom on iOS.
- **App lifecycle.** When the app goes to the background, the client sends its queue, ends the session if sessions are
  on, and pauses. It resumes when the app becomes active.
- **A persistent queue.** Unsent events are stored with AsyncStorage and sent after the app restarts. If AsyncStorage
  can't be loaded, the client keeps events in memory.

## Next steps

- [Send logs and feature usage](/docs/clients/javascript/sending-events/) from screens.
- [Change log levels and turn off events without shipping an update](/docs/clients/javascript/client-configuration-values/).
- [Remove private data](/docs/clients/javascript/client-configuration/#general-data-protection-regulation) before it
  leaves the device.
- Run the [Expo example app](https://github.com/exceptionless/Exceptionless.JavaScript/tree/main/example/expo).

## Ask Exie

```prompt exie
Which OS versions had the most errors in the last 7 days?
```

```prompt exie
Which app versions had the most errors this week?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can get your project's setup instructions and
do the install:

```prompt agent
Use the Exceptionless MCP server to get the Expo client setup instructions for my project, then add Exceptionless to this app: install the packages, add the config plugin to app.json, start the client in App.tsx, and wrap the app in ExceptionlessErrorBoundary.
```
