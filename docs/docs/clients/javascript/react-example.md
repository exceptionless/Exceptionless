---
title: "React Example"
---

# React Example

The React Client from Exceptionless includes all of the functionality from the browser client, but with some extra React-specific helpers.

The one thing you'll specifically notice in the React client is the addition of an `ExceptionlessErrorBoundary` class component. This is a wrapper component that can be used to ensure all errors in the presentational layer of your app are reported.

It works exactly as Error Boundaries in React work, but it's pre-wired to report to Exceptionless. Here's a very simple example:

```js
import React from 'react';
import ReactDOM from 'react-dom';
import './index.css';
import App from './App';
import reportWebVitals from './reportWebVitals';
import {
  Exceptionless,
  ExceptionlessErrorBoundary
} from "@exceptionless/react";
import ErrorBoundary from './ErrorBoundary';

const startExceptionless = async () => {
  await Exceptionless.startup((c) => {
    c.apiKey = "YOUR API KEY";
    c.useDebugLogger();

    c.defaultTags.push("Example", "React");
  });
};

startExceptionless();

ReactDOM.render(
  <React.StrictMode>
    <ErrorBoundary>
      <ExceptionlessErrorBoundary>
        <App />
      </ExceptionlessErrorBoundary>
    </ErrorBoundary>
  </React.StrictMode>,
  document.getElementById('root')
);

reportWebVitals();
```

As you can see, we wrap the root `App` component first in the `ExceptionlessErrorBoundary` and then if you want to present your own fallback UI, you can create a custom `ErrorBoundary` component that will make sure that UI is served.

We have a detailed [blog post about this here](https://exceptionless.com/news/2021/2021-08-16-how-to-use-error-boundaries-in-react/).

[The full example / sample can be found here](https://github.com/exceptionless/Exceptionless.JavaScript/tree/master/example/react).

---

[Next > Node.js Example](/docs/clients/javascript/node-example) {.text-right }
