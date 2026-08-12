import { browser, building } from '$app/environment';
import { afterNavigate, beforeNavigate, pushState, replaceState } from '$app/navigation';
import { page } from '$app/state';
import { onDestroy } from 'svelte';

import type { CreateQueryParametersOptions, QueryParameterSchema, QueryParameterState } from './types.js';

import { createQueryParameterProxy } from './proxy.js';
import { applyQueryParameterUpdates, createDebouncedFunction, createSearchParams, parseQueryParameters, searchParamsEqual } from './query-params.js';

export function createQueryParameters<T extends QueryParameterSchema>({
    debounceMilliseconds = 200,
    defaults,
    history = 'replace',
    schema
}: CreateQueryParametersOptions<T>) {
    let searchParams = createSearchParams(building ? '' : page.url.search);
    const current = $state<QueryParameterState<T>>(parseQueryParameters(searchParams, schema, defaults));
    // Create the Back target synchronously, then replace it while rapid updates still
    // belong to the same user interaction. Deferring the first push leaves no target
    // for an immediate Back action and can navigate a newly opened tab to about:blank.
    let isCoalescingPushHistoryEntry = false;
    let coalescingStartUrl: string | undefined;
    const schedulePushHistoryEntrySettlement = createDebouncedFunction(() => {
        isCoalescingPushHistoryEntry = false;
        coalescingStartUrl = undefined;
    }, debounceMilliseconds);
    const getCurrentUrl = () => {
        const query = createSearchParams(window.location.search).toString();
        return `${window.location.pathname}${query ? `?${query}` : ''}${window.location.hash}`;
    };

    const synchronizeURL = () => {
        if (searchParamsEqual(searchParams, window.location.search)) {
            return;
        }

        const query = searchParams.toString();
        const url = `${window.location.pathname}${query ? `?${query}` : ''}${window.location.hash}`;
        if (history === 'replace') {
            replaceState(url, page.state);
        } else if (!isCoalescingPushHistoryEntry) {
            coalescingStartUrl = getCurrentUrl();
            pushState(url, page.state);
            isCoalescingPushHistoryEntry = true;
        } else if (url === coalescingStartUrl) {
            // Keep the transient state as a meaningful Back target instead of
            // replacing it with a duplicate of the entry behind it.
            pushState(url, page.state);
            isCoalescingPushHistoryEntry = false;
            coalescingStartUrl = undefined;
        } else {
            replaceState(url, page.state);
        }

        if (history === 'push') {
            schedulePushHistoryEntrySettlement();
        }
    };

    const settlePushHistoryEntry = () => {
        schedulePushHistoryEntrySettlement.cancel();
        isCoalescingPushHistoryEntry = false;
        coalescingStartUrl = undefined;
    };

    beforeNavigate(settlePushHistoryEntry);
    onDestroy(settlePushHistoryEntry);

    const commit = (result: ReturnType<typeof applyQueryParameterUpdates<T>>) => {
        searchParams = result.searchParams;
        if (result.stateChanged) {
            Object.assign(current, result.state);
        }

        if (result.urlChanged) {
            synchronizeURL();
        }
    };

    const update = (values: Parameters<typeof applyQueryParameterUpdates<T>>[2]) => {
        commit(applyQueryParameterUpdates(current, searchParams, values, schema));
    };

    const synchronizeState = (search: string) => {
        const nextSearchParams = createSearchParams(search);
        if (searchParamsEqual(searchParams, nextSearchParams)) {
            return;
        }

        searchParams = nextSearchParams;
        Object.assign(current, parseQueryParameters(searchParams, schema, defaults));
    };

    const synchronizeStateFromLocation = () => synchronizeState(window.location.search);
    if (browser) {
        window.addEventListener('popstate', synchronizeStateFromLocation);
    }

    onDestroy(() => {
        if (browser) {
            window.removeEventListener('popstate', synchronizeStateFromLocation);
        }
    });

    afterNavigate(({ to }) => {
        if (to) {
            synchronizeState(to.url.search);
        }
    });

    return createQueryParameterProxy(current, schema, { update });
}
