# Query parameters

Exceptionless's shared Svelte query-parameter state module. It is intentionally tailored to the application's current needs:

- top-level string, number, boolean, date, and enum parameters;
- typed property access and atomic multi-parameter updates;
- preservation of unrelated URL parameters;
- debounced push or replace navigation;
- synchronization with browser navigation;
- no state, URL, or history writes for unchanged values after coercion;
- cancellation of pending synchronization during navigation and component teardown.

```ts
import { createQueryParameters } from '$shared/query-params';

const queryParams = createQueryParameters({
    defaults: { page: 1 },
    history: 'push',
    schema: {
        filter: 'string',
        page: 'number'
    }
});

queryParams.update({ filter: 'status:open', page: 1 });
```

Updates may also assign a single schema property directly. Use `update()` when several parameters form one logical state change so they produce one reactive update and one URL synchronization.

The implementation was originally derived from [beynar/kit-query-params](https://github.com/beynar/kit-query-params) version 0.0.26 at commit `7c90edf7`. The original copyright and MIT license are retained in [LICENSE](./LICENSE). This module is maintained as first-party Exceptionless code and does not track the upstream package API.
