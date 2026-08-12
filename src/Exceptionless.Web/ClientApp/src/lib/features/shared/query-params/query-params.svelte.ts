import { browser, building } from '$app/environment';
import { afterNavigate, beforeNavigate, pushState, replaceState } from '$app/navigation';
import { page } from '$app/state';
import { onDestroy } from 'svelte';

import type { CreateQueryParametersOptions, QueryParameterSchema, QueryParameterState } from './types.js';

import { createQueryParameterProxy } from './proxy.js';
import { applyQueryParameterUpdates, createDebouncedFunction, createSearchParams, parseQueryParameters, searchParamsEqual } from './query-params.js';

interface HistoryEntrySnapshot {
    state: App.PageState;
    url: string;
}

export function createQueryParameters<T extends QueryParameterSchema>({
    debounceMilliseconds = 200,
    defaults,
    history = 'replace',
    schema
}: CreateQueryParametersOptions<T>) {
    let searchParams = createSearchParams(building ? '' : page.url.search);
    const current = $state<QueryParameterState<T>>(parseQueryParameters(searchParams, schema, defaults));
    let pendingPushHistoryEntry: HistoryEntrySnapshot | undefined;
    const getCurrentUrl = () => `${window.location.pathname}${window.location.search}${window.location.hash}`;

    const finalizePushHistoryEntry = () => {
        const previousEntry = pendingPushHistoryEntry;
        pendingPushHistoryEntry = undefined;
        if (!previousEntry) {
            return;
        }

        const currentEntry = { state: page.state, url: getCurrentUrl() };
        if (currentEntry.url === previousEntry.url) {
            return;
        }

        replaceState(previousEntry.url, previousEntry.state);
        pushState(currentEntry.url, currentEntry.state);
    };

    const schedulePushHistoryEntryFinalization = createDebouncedFunction(finalizePushHistoryEntry, debounceMilliseconds);
    const flushPendingPushHistoryEntry = () => {
        schedulePushHistoryEntryFinalization.cancel();
        finalizePushHistoryEntry();
    };

    const synchronizeURL = () => {
        if (searchParamsEqual(searchParams, window.location.search)) {
            return;
        }

        const query = searchParams.toString();
        const url = `${window.location.pathname}${query ? `?${query}` : ''}${window.location.hash}`;
        if (history === 'replace') {
            replaceState(url, page.state);
            return;
        }

        if (!pendingPushHistoryEntry) {
            pendingPushHistoryEntry = { state: page.state, url: getCurrentUrl() };
        }

        replaceState(url, page.state);
        schedulePushHistoryEntryFinalization();
    };

    beforeNavigate(flushPendingPushHistoryEntry);
    onDestroy(flushPendingPushHistoryEntry);

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
