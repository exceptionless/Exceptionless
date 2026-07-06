---
title: "React"
order: 1
---

# React

Exceptionless can be configured in just about any JavaScript environment, but this section is dedicated to set up and use within the React framework.

### Install

To install exceptionless, you can use npm or yarn:

npm - `npm install @exceptionless/react`

yarn - `yarn add @exceptionless/react`

### Initializing the Client

Exceptionless provides a default singleton client instance. While we recommend
using the default client instance for most use cases, you can also create
custom instances (though that's beyond the scope of this guide).

```javascript
import { Exceptionless } from "@exceptionless/react";

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR API KEY";
});
```

You can see an additional parameter passed into the configuration object as an
example. To see all the available options, take a look at our
[configuration values here](/docs/clients/javascript/client-configuration-values).

### Using Exceptionless in a React App

```javascript
import { Exceptionless, ExceptionlessErrorBoundary } from "@exceptionless/react";

class App extends Component {1
  async componentDidMount() {
    await Exceptionless.startup((c) => {
      c.apiKey = "YOUR API KEY";
    });
  }

  render() {
    return (
      <ExceptionlessErrorBoundary>
        
// YOUR APP COMPONENTS HERE

      </ExceptionlessErrorBoundary>
    );
  }
}

export default App;
```

With that set up, you can use the Exceptionless client anywhere in your app.

---

[Next > Vue](/docs/clients/javascript/guides/vue)
