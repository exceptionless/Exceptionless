import { browser, building } from '$app/environment';
import { afterNavigate, beforeNavigate, pushState, replaceState } from '$app/navigation';
import { page } from '$app/state';
import { onDestroy } from 'svelte';
import { SvelteURL } from 'svelte/reactivity';

import type { CreateQueryParametersOptions, QueryParameterSchema, QueryParameterState } from './types.js';

import { createQueryParameterProxy } from './proxy.js';
import { applyQueryParameterUpdates, createDebouncedFunction, createSearchParams, parseQueryParameters, searchParamsEqual } from './query-params.js';

const queryHistoryEntryIdKey = '__exceptionlessQueryHistoryEntryId';
const pendingReplacementStoragePrefix = 'exceptionless:query-history:';

type QueryHistoryPageState = App.PageState & { [queryHistoryEntryIdKey]?: string };

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
    let coalescingEntryId: string | undefined;
    let pendingReplacementUrl: string | undefined;
    const normalizeUrl = (url: string) => {
        const value = new SvelteURL(url, window.location.origin);
        value.searchParams.sort();
        const query = value.searchParams.toString();

        return `${value.pathname}${query ? `?${query}` : ''}${value.hash}`;
    };

    const getCurrentUrl = () => normalizeUrl(`${window.location.pathname}${window.location.search}${window.location.hash}`);

    const getCurrentHistoryEntryId = () => (window.history.state as null | QueryHistoryPageState)?.[queryHistoryEntryIdKey];

    const createHistoryState = (entryId: string | undefined) => ({ ...page.state, ...(entryId ? { [queryHistoryEntryIdKey]: entryId } : {}) }) as App.PageState;

    const getPendingReplacementStorageKey = (entryId: string) => `${pendingReplacementStoragePrefix}${entryId}`;

    if (browser) {
        const currentHistoryEntryId = getCurrentHistoryEntryId();
        const retainedReplacementUrl = currentHistoryEntryId ? sessionStorage.getItem(getPendingReplacementStorageKey(currentHistoryEntryId)) : undefined;
        if (currentHistoryEntryId && retainedReplacementUrl) {
            sessionStorage.removeItem(getPendingReplacementStorageKey(currentHistoryEntryId));
            replaceState(retainedReplacementUrl, createHistoryState(currentHistoryEntryId));
            searchParams = createSearchParams(window.location.search);
            Object.assign(current, parseQueryParameters(searchParams, schema, defaults));
        }
    }

    const settlePushHistoryEntry = () => {
        isCoalescingPushHistoryEntry = false;
        coalescingStartUrl = undefined;
        coalescingEntryId = undefined;
    };

    const discardPendingReplacement = () => {
        if (browser && coalescingEntryId) {
            sessionStorage.removeItem(getPendingReplacementStorageKey(coalescingEntryId));
        }

        pendingReplacementUrl = undefined;
    };

    const flushPendingReplacement = () => {
        if (pendingReplacementUrl) {
            replaceState(pendingReplacementUrl, createHistoryState(coalescingEntryId));
            discardPendingReplacement();
        }
    };

    const finalizePushHistoryEntry = () => {
        if (!coalescingEntryId || getCurrentHistoryEntryId() === coalescingEntryId) {
            flushPendingReplacement();
            settlePushHistoryEntry();
        }
    };

    const schedulePushHistoryEntryFinalization = createDebouncedFunction(finalizePushHistoryEntry, debounceMilliseconds);

    const synchronizeURL = () => {
        if (searchParamsEqual(searchParams, window.location.search)) {
            discardPendingReplacement();
            return;
        }

        if (history === 'push' && isCoalescingPushHistoryEntry && getCurrentHistoryEntryId() !== coalescingEntryId) {
            // A popstate traversal may retain a pending replacement for the entry
            // we left. Editing this destination discards that Forward entry, so
            // start a fresh burst here instead of mutating the retained source.
            schedulePushHistoryEntryFinalization.cancel();
            discardPendingReplacement();
            settlePushHistoryEntry();
        }

        const query = searchParams.toString();
        const url = `${window.location.pathname}${query ? `?${query}` : ''}${window.location.hash}`;
        if (history === 'replace') {
            replaceState(url, page.state);
        } else if (!isCoalescingPushHistoryEntry) {
            coalescingStartUrl = getCurrentUrl();
            coalescingEntryId = crypto.randomUUID();
            pushState(url, createHistoryState(coalescingEntryId));
            isCoalescingPushHistoryEntry = true;
        } else if (normalizeUrl(url) === coalescingStartUrl) {
            // Keep the transient state as a meaningful Back target instead of
            // replacing it with a duplicate of the entry behind it.
            schedulePushHistoryEntryFinalization.cancel();
            discardPendingReplacement();
            pushState(url, page.state);
            settlePushHistoryEntry();
        } else {
            // Avoid exhausting browser History API mutation quotas during sustained input.
            pendingReplacementUrl = url;
            if (browser && coalescingEntryId) {
                sessionStorage.setItem(getPendingReplacementStorageKey(coalescingEntryId), url);
            }
        }

        if (history === 'push' && isCoalescingPushHistoryEntry) {
            schedulePushHistoryEntryFinalization();
        }
    };

    const handleBeforeNavigate = ({ type }: { type: string }) => {
        schedulePushHistoryEntryFinalization.cancel();
        if (type === 'popstate') {
            if (pendingReplacementUrl && getCurrentHistoryEntryId() === coalescingEntryId) {
                flushPendingReplacement();
                settlePushHistoryEntry();
            }

            return;
        }

        if (getCurrentHistoryEntryId() === coalescingEntryId) {
            flushPendingReplacement();
        } else {
            discardPendingReplacement();
        }

        settlePushHistoryEntry();
    };

    const handleBeforeUnload = () => {
        schedulePushHistoryEntryFinalization.cancel();
        finalizePushHistoryEntry();
    };

    beforeNavigate(handleBeforeNavigate);
    if (browser) {
        window.addEventListener('beforeunload', handleBeforeUnload);
    }

    onDestroy(finalizePushHistoryEntry);

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
            window.removeEventListener('beforeunload', handleBeforeUnload);
        }
    });

    afterNavigate(() => {
        if (browser) {
            synchronizeStateFromLocation();
        }
    });

    return createQueryParameterProxy(current, schema, { update });
}
