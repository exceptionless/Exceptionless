---
title: "Angular"
order: 3
---

# Angular

Exceptionless can be configured in just about any JavaScript environment, but this section is dedicated to set up and use within the Angular framework.

### Install

To install exceptionless, you can use npm or yarn:

npm - `npm install @exceptionless/browser`

yarn - `yarn add @exceptionless/browser`

### Initializing the Client

Exceptionless provides a default singleton client instance. While we recommend
using the default client instance for most use cases, you can also create
custom instances (though that's beyond the scope of this guide).

```javascript
import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR API KEY";
});
```

You can see an additional parameter passed into the configuration object as an
example. To see all the available options, take a look at our
[configuration values here](/docs/clients/javascript/client-configuration-values).

### Using Exceptionless in an Angular Component

To make use of Exceptionless within a component, you'll import the package like
described above. Your set up will vary depending on your needs, but this is a
quick example of using Exceptionless within the `app` component of a default
Angular project.

```js
import { Component } from '@angular/core';
import { Exceptionless } from "@exceptionless/browser";

Exceptionless.startup((c) => {
  c.apiKey = "YOUR API KEY";
});

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent {
  title = 'my-app';
  public handleClick(event) {
    try {
      throw new Error("Whoops!");
    } catch (error) {
      Exceptionless.submitException(error);
    }
  }
}
```

In the `app` component's html, clicking a button that calls `handleClick` will immediately throw an error and report it to Exceptionless.

---

[Next > Express](/docs/clients/javascript/guides/express)
