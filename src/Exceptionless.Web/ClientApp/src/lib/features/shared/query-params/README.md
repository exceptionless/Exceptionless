# Query parameters

Exceptionless's shared Svelte query-parameter state module. It is intentionally tailored to the application's current needs:

- top-level string, number, boolean, date, and enum parameters, and lists of each;
- typed property access and atomic multi-parameter updates;
- preservation of unrelated URL parameters, including those written by other instances on the same page;
- immediate, reload-safe URL synchronization;
- coalescing of rapid push-history updates into one Back-button entry;
- synchronization with browser navigation;
- no state, URL, or history writes for unchanged values after coercion;
- settlement of history-entry coalescing during navigation and component teardown.

```ts
import { createQueryParameters } from '#shared/query-params/index.ts';

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

Append `[]` to a type for a list parameter, such as `'string[]'`, `'number[]'`, or `'<open,fixed>[]'`. Lists use repeated keys (`status=open&status=fixed`), drop invalid and duplicate items when parsed, and are never null. List values are frozen; assign a new array instead of mutating one, because only assignment updates the URL. An emptied list removes its key, unless the list has a non-empty default; then it writes an empty value (`status=`) so a reload keeps the list empty instead of restoring the default.

Each instance owns only its schema's keys. Writes apply those keys onto the live URL, so several instances on one page do not overwrite each other's parameters.

Updates may also assign a single schema property directly. Use `update()` when several parameters form one logical state change so they produce one reactive update and one URL synchronization.

With `history: 'push'`, the first update immediately pushes a durable Back-button entry. When the current entry is a transient detail-sheet entry, the update replaces that entry instead so the prior list remains the direct Back target. Rapid follow-up updates are coalesced into one replacement after `debounceMilliseconds`, avoiding browser History API mutation limits, and a pending replacement is flushed before reload or teardown. If a burst returns to its starting URL, comparison is independent of query encoding and parameter order, and the return is pushed so the transient state remains a meaningful Back target rather than creating adjacent duplicate URLs. Immediate Back keeps a pending replacement associated with its unique source entry; Forward restores the latest value after route teardown or reload without rewriting the Back destination or replaying it onto a later visit to the same URL. Session-storage persistence is best-effort; if browser policy denies storage, current-page coalescing continues without blocking hydration. With `history: 'replace'`, every update immediately replaces the current entry.

History writes are SvelteKit shallow navigations made through `updateShallowHistory` (`../history-state.ts`): they persist page state across reloads, wait for any client-side navigation in progress (a shallow `goto` would abort it), and are ignored by this module's own navigation hooks. Because SvelteKit applies an entry's page state after the `popstate` event, Back and Forward are handled once that state changes. A pending replacement is not written while the page is navigating away; it stays with its entry and is applied if the entry is revisited.

The implementation was originally derived from [beynar/kit-query-params](https://github.com/beynar/kit-query-params) version 0.0.26 at commit `7c90edf7`. The original copyright and MIT license are retained in [LICENSE](./LICENSE). This module is maintained as first-party Exceptionless code and does not track the upstream package API.
