import { browser, building } from '$app/environment';
import { afterNavigate, beforeNavigate, pushState, replaceState } from '$app/navigation';
import { page } from '$app/state';
import { onDestroy } from 'svelte';
import { SvelteURL } from 'svelte/reactivity';

import type { CreateQueryParametersOptions, QueryParameterHistory, QueryParameterSchema, QueryParameterState } from './types.js';

import { hasDetailSheetHistoryEntry, withoutDetailSheetHistoryEntry } from '../history-state.js';
import { createQueryParameterProxy } from './proxy.js';
import { applyQueryParameterUpdates, createDebouncedFunction, createSearchParams, parseQueryParameters, searchParamsEqual } from './query-params.js';

const queryHistoryEntryIdKey = '__exceptionlessQueryHistoryEntryId';
const pendingReplacementStoragePrefix = 'exceptionless:query-history:';
const svelteKitPageStateKey = 'sveltekit:states';

type QueryHistoryPageState = App.PageState & { [queryHistoryEntryIdKey]?: string };
type SvelteKitHistoryState = { [svelteKitPageStateKey]?: QueryHistoryPageState };

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

    const getCurrentHistoryEntryId = () => (window.history.state as null | SvelteKitHistoryState)?.[svelteKitPageStateKey]?.[queryHistoryEntryIdKey];

    const createHistoryState = (entryId: string | undefined, state: App.PageState = page.state) =>
        ({
            ...state,
            ...(entryId
                ? {
                      [queryHistoryEntryIdKey]: entryId
                  }
                : {})
        }) as App.PageState;

    const getPendingReplacementStorageKey = (entryId: string) => `${pendingReplacementStoragePrefix}${entryId}`;
    const getStoredPendingReplacement = (entryId: string) => {
        try {
            return sessionStorage.getItem(getPendingReplacementStorageKey(entryId)) ?? undefined;
        } catch {
            return undefined;
        }
    };

    const removeStoredPendingReplacement = (entryId: string) => {
        try {
            sessionStorage.removeItem(getPendingReplacementStorageKey(entryId));
        } catch {
            // Storage can be denied by browser policy; in-memory coalescing still works.
        }
    };

    const storePendingReplacement = (entryId: string, url: string) => {
        try {
            sessionStorage.setItem(getPendingReplacementStorageKey(entryId), url);
        } catch {
            // Storage can be denied by browser policy; in-memory coalescing still works.
        }
    };

    if (browser) {
        const currentHistoryEntryId = getCurrentHistoryEntryId();
        const retainedReplacementUrl = currentHistoryEntryId ? getStoredPendingReplacement(currentHistoryEntryId) : undefined;
        if (currentHistoryEntryId && retainedReplacementUrl) {
            removeStoredPendingReplacement(currentHistoryEntryId);
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
            removeStoredPendingReplacement(coalescingEntryId);
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

    const synchronizeURL = (historyMode = history) => {
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
        if (historyMode === 'replace') {
            schedulePushHistoryEntryFinalization.cancel();
            discardPendingReplacement();
            settlePushHistoryEntry();
            replaceState(url, page.state);
        } else if (!isCoalescingPushHistoryEntry) {
            coalescingStartUrl = getCurrentUrl();
            coalescingEntryId = crypto.randomUUID();
            if (hasDetailSheetHistoryEntry(page.state)) {
                // The detail sheet already pushed a same-URL entry. Replace that
                // transient entry so a filter applied from the sheet has the
                // original list as its direct Back target.
                replaceState(url, createHistoryState(coalescingEntryId, withoutDetailSheetHistoryEntry(page.state)));
            } else {
                pushState(url, createHistoryState(coalescingEntryId));
            }

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
                storePendingReplacement(coalescingEntryId, url);
            }
        }

        if (history === 'push' && isCoalescingPushHistoryEntry) {
            schedulePushHistoryEntryFinalization();
        }
    };

    const handleBeforeNavigate = ({ type }: { type: string }) => {
        schedulePushHistoryEntryFinalization.cancel();
        if (type === 'popstate') {
            if (coalescingEntryId && getCurrentHistoryEntryId() === coalescingEntryId) {
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

    const commit = (result: ReturnType<typeof applyQueryParameterUpdates<T>>, historyMode?: QueryParameterHistory) => {
        searchParams = result.searchParams;
        if (result.stateChanged) {
            Object.assign(current, result.state);
        }

        if (result.urlChanged) {
            synchronizeURL(historyMode);
        }
    };

    const update = (values: Parameters<typeof applyQueryParameterUpdates<T>>[2], historyMode?: QueryParameterHistory) => {
        commit(applyQueryParameterUpdates(current, searchParams, values, schema), historyMode);
    };

    const synchronizeState = (search: string) => {
        const nextSearchParams = createSearchParams(search);
        if (searchParamsEqual(searchParams, nextSearchParams)) {
            return;
        }

        searchParams = nextSearchParams;
        Object.assign(current, parseQueryParameters(searchParams, schema, defaults));
    };

    const synchronizeStateFromLocation = () => {
        schedulePushHistoryEntryFinalization.cancel();
        if (coalescingEntryId && getCurrentHistoryEntryId() === coalescingEntryId) {
            flushPendingReplacement();
            settlePushHistoryEntry();
        }

        synchronizeState(window.location.search);
    };

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

    return createQueryParameterProxy(current, schema, {
        update
    });
}
