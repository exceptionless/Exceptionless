import type { BeforeNavigate } from '$app/navigation';

import { browser, building } from '$app/env';
import { afterNavigate, beforeNavigate } from '$app/navigation';
import { page } from '$app/state';
import { onDestroy, untrack } from 'svelte';
import { SvelteURL } from 'svelte/reactivity';

import type { CreateQueryParametersOptions, QueryParameterSchema, QueryParameterState, QueryParameterUpdateOptions } from './types.js';

import { hasDetailSheetHistoryEntry, updateShallowHistory, waitForShallowHistory, withoutDetailSheetHistoryEntry } from '../history-state.js';
import { getHistoryTraversalCount } from '../history-traversal.js';
import { createQueryParameterProxy } from './proxy.js';
import {
    applyQueryParameterUpdates,
    createDebouncedFunction,
    createSearchParams,
    mergeQueryParameters,
    parseQueryParameters,
    queryParameterValuesEqual,
    searchParamsEqual
} from './query-params.js';

const queryHistoryEntryIdKey = '__exceptionlessQueryHistoryEntryId';
const pendingReplacementStoragePrefix = 'exceptionless:query-history:';

type QueryHistoryPageState = App.PageState & { [queryHistoryEntryIdKey]?: string };

interface QueryHistoryWrite {
    entryId?: string;
    replace?: boolean;
    withoutDetailSheet?: boolean;
}

export function createQueryParameters<T extends QueryParameterSchema>({
    debounceMilliseconds = 200,
    defaults,
    history = 'replace',
    schema
}: CreateQueryParametersOptions<T>) {
    // `page.url` is not updated by shallow history writes, so it can be older than the visible URL.
    let searchParams = createSearchParams(browser ? window.location.search : building ? '' : page.url.search);
    const current = createQueryParameterState(parseQueryParameters(searchParams, schema, defaults));
    // Create the Back target synchronously, then replace it while rapid updates still
    // belong to the same user interaction. Deferring the first push leaves no target
    // for an immediate Back action and can navigate a newly opened tab to about:blank.
    let isCoalescingPushHistoryEntry = false;
    let isWritingCoalescingEntry = false;
    let coalescingStartUrl: string | undefined;
    let coalescingEntryId: string | undefined;
    let pendingReplacementUrl: string | undefined;
    // Back and Forward are handled once SvelteKit has applied the destination's page state.
    let handledHistoryTraversals = getHistoryTraversalCount();
    const normalizeUrl = (url: string) => {
        const value = new SvelteURL(url, window.location.origin);
        value.searchParams.sort();
        const query = value.searchParams.toString();

        return `${value.pathname}${query ? `?${query}` : ''}${value.hash}`;
    };

    const getCurrentUrl = () => normalizeUrl(`${window.location.pathname}${window.location.search}${window.location.hash}`);

    // Other query-parameter instances and unrelated code can write the URL between this
    // instance's updates, so writes apply this instance's keys onto the live URL.
    const createLocationSearchParams = (ownSearchParams: URLSearchParams) => mergeQueryParameters(window.location.search, ownSearchParams, schema);

    const createLocationUrl = (ownSearchParams: URLSearchParams) => {
        const query = createLocationSearchParams(ownSearchParams).toString();
        return `${window.location.pathname}${query ? `?${query}` : ''}${window.location.hash}`;
    };

    const rebaseUrl = (url: string) => createLocationUrl(new SvelteURL(url, window.location.origin).searchParams);

    const getCurrentHistoryEntryId = () => (page.state as QueryHistoryPageState)[queryHistoryEntryIdKey];

    // The burst's first push is asynchronous, so the entry it creates may not be current yet.
    const isOnCoalescingEntry = () => !!coalescingEntryId && (isWritingCoalescingEntry || getCurrentHistoryEntryId() === coalescingEntryId);

    const writeHistory = (url: () => string, { entryId, replace = false, withoutDetailSheet = false }: QueryHistoryWrite = {}) =>
        updateShallowHistory({
            replace,
            state: (state) => {
                const nextState = withoutDetailSheet ? withoutDetailSheetHistoryEntry(state) : state;
                return entryId
                    ? ({
                          ...nextState,
                          [queryHistoryEntryIdKey]: entryId
                      } as App.PageState)
                    : nextState;
            },
            url
        });

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
            // Render the retained value now; the URL is written once the navigation that created
            // this component has finished.
            searchParams = createSearchParams(new SvelteURL(rebaseUrl(retainedReplacementUrl), window.location.origin).search);
            assignChangedValues(parseQueryParameters(searchParams, schema, defaults));
            void writeHistory(() => createLocationUrl(searchParams), {
                entryId: currentHistoryEntryId,
                replace: true
            });
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
            const url = pendingReplacementUrl;
            void writeHistory(() => rebaseUrl(url), {
                entryId: coalescingEntryId,
                replace: true
            });
            discardPendingReplacement();
        }
    };

    const finalizePushHistoryEntry = () => {
        if (!coalescingEntryId || isOnCoalescingEntry()) {
            flushPendingReplacement();
            settlePushHistoryEntry();
        }
    };

    const schedulePushHistoryEntryFinalization = createDebouncedFunction(finalizePushHistoryEntry, debounceMilliseconds);

    const synchronizeURL = (updateHistory = history) => {
        handledHistoryTraversals = getHistoryTraversalCount();
        if (searchParamsEqual(createLocationSearchParams(searchParams), window.location.search)) {
            discardPendingReplacement();
            return;
        }

        if (updateHistory === 'replace') {
            schedulePushHistoryEntryFinalization.cancel();
            discardPendingReplacement();
            settlePushHistoryEntry();
        } else if (isCoalescingPushHistoryEntry && !isOnCoalescingEntry()) {
            // A popstate traversal may retain a pending replacement for the entry
            // we left. Editing this destination discards that Forward entry, so
            // start a fresh burst here instead of mutating the retained source.
            schedulePushHistoryEntryFinalization.cancel();
            discardPendingReplacement();
            settlePushHistoryEntry();
        }

        const url = createLocationUrl(searchParams);
        const writeCurrentUrl = () => createLocationUrl(searchParams);
        if (updateHistory === 'replace') {
            void writeHistory(writeCurrentUrl, {
                replace: true
            });
        } else if (!isCoalescingPushHistoryEntry) {
            coalescingStartUrl = getCurrentUrl();
            coalescingEntryId = crypto.randomUUID();
            isWritingCoalescingEntry = true;
            // The detail sheet already pushed a same-URL entry. Replace that transient
            // entry so a filter applied from the sheet has the original list as its
            // direct Back target.
            const replaceDetailSheetEntry = hasDetailSheetHistoryEntry(page.state);
            void writeHistory(writeCurrentUrl, {
                entryId: coalescingEntryId,
                replace: replaceDetailSheetEntry,
                withoutDetailSheet: replaceDetailSheetEntry
            }).finally(() => {
                isWritingCoalescingEntry = false;
            });

            isCoalescingPushHistoryEntry = true;
        } else if (normalizeUrl(url) === coalescingStartUrl) {
            // Keep the transient state as a meaningful Back target instead of
            // replacing it with a duplicate of the entry behind it.
            schedulePushHistoryEntryFinalization.cancel();
            discardPendingReplacement();
            void writeHistory(writeCurrentUrl);
            settlePushHistoryEntry();
        } else {
            // Avoid exhausting browser History API mutation quotas during sustained input.
            pendingReplacementUrl = url;
            if (browser && coalescingEntryId) {
                storePendingReplacement(coalescingEntryId, url);
            }
        }

        if (updateHistory === 'push' && isCoalescingPushHistoryEntry) {
            schedulePushHistoryEntryFinalization();
        }
    };

    const handleBeforeNavigate = ({ shallow, type, willUnload }: BeforeNavigate) => {
        if (shallow && type === 'goto') {
            return;
        }

        schedulePushHistoryEntryFinalization.cancel();
        if (type === 'popstate') {
            return;
        }

        if (willUnload) {
            finalizePushHistoryEntry();
            return;
        }

        // Writing history now would abort the client-side navigation that is starting, so a
        // pending replacement stays retained for its entry and is applied if that entry is
        // revisited.
        if (isOnCoalescingEntry()) {
            pendingReplacementUrl = undefined;
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

    const commit = (result: ReturnType<typeof applyQueryParameterUpdates<T>>, options?: QueryParameterUpdateOptions) => {
        searchParams = result.searchParams;
        if (result.stateChanged) {
            assignChangedValues(result.state);
        }

        if (result.urlChanged) {
            synchronizeURL(options?.history);
        }
    };

    const update = (values: Parameters<typeof applyQueryParameterUpdates<T>>[2], options?: QueryParameterUpdateOptions) => {
        commit(applyQueryParameterUpdates(current, searchParams, values, schema, defaults), options);
    };

    const synchronizeState = (search: string) => {
        const nextSearchParams = createSearchParams(search);
        if (searchParamsEqual(searchParams, nextSearchParams)) {
            return;
        }

        searchParams = nextSearchParams;
        assignChangedValues(parseQueryParameters(searchParams, schema, defaults));
    };

    const synchronizeStateFromLocation = () => {
        schedulePushHistoryEntryFinalization.cancel();
        // The flushed replacement is written asynchronously, so read state from it directly.
        const flushedUrl = isOnCoalescingEntry() ? pendingReplacementUrl : undefined;
        if (isOnCoalescingEntry()) {
            flushPendingReplacement();
            settlePushHistoryEntry();
        }

        synchronizeState(flushedUrl ? new SvelteURL(rebaseUrl(flushedUrl), window.location.origin).search : window.location.search);
    };

    $effect(() => {
        // Tracks the entry's page state, which SvelteKit applies when Back or Forward traverses history.
        void getCurrentHistoryEntryId();
        untrack(() => {
            const traversals = getHistoryTraversalCount();
            if (traversals !== handledHistoryTraversals) {
                handledHistoryTraversals = traversals;
                synchronizeStateFromLocation();
            }
        });
    });

    onDestroy(() => {
        if (browser) {
            window.removeEventListener('beforeunload', handleBeforeUnload);
        }
    });

    afterNavigate(({ shallow, type }) => {
        if (shallow && type === 'goto') {
            return;
        }

        // Writes made while the page rendered, such as hydrating saved-view filters, wait for the
        // navigation to finish, so read the URL once they are applied.
        if (browser) {
            void waitForShallowHistory().then(synchronizeStateFromLocation);
        }
    });

    return createQueryParameterProxy(current, schema, {
        update
    });

    function assignChangedValues(next: QueryParameterState<T>) {
        for (const key of Object.keys(schema) as (keyof T)[]) {
            if (!queryParameterValuesEqual(current[key], next[key])) {
                current[key] = next[key];
            }
        }
    }
}

/**
 * Backs each parameter with its own raw signal: readers stay fine-grained, and frozen
 * list values are stored as-is instead of being wrapped in a deep state proxy.
 */
function createQueryParameterState<T extends QueryParameterSchema>(initial: QueryParameterState<T>): QueryParameterState<T> {
    const state = {} as QueryParameterState<T>;
    for (const key of Object.keys(initial) as (keyof T)[]) {
        let value = $state.raw(initial[key]);
        Object.defineProperty(state, key, {
            enumerable: true,
            get: () => value,
            set: (next: QueryParameterState<T>[keyof T]) => {
                value = next;
            }
        });
    }

    return state;
}
