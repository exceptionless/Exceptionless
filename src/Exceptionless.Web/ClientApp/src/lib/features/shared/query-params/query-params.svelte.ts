import { browser, building } from '$app/environment';
import { afterNavigate, beforeNavigate, pushState, replaceState } from '$app/navigation';
import { page } from '$app/state';
import { onDestroy } from 'svelte';
import { SvelteMap, SvelteURL } from 'svelte/reactivity';

import type { CreateQueryParametersOptions, QueryParameterSchema, QueryParameterState } from './types.js';

import { createQueryParameterProxy } from './proxy.js';
import { applyQueryParameterUpdates, createDebouncedFunction, createSearchParams, parseQueryParameters, searchParamsEqual } from './query-params.js';

const pendingPushHistoryReplacements = new SvelteMap<string, string>();

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
    let coalescingEntryUrl: string | undefined;
    let pendingReplacementUrl: string | undefined;
    const normalizeUrl = (url: string) => {
        const value = new SvelteURL(url, window.location.origin);
        value.searchParams.sort();
        const query = value.searchParams.toString();

        return `${value.pathname}${query ? `?${query}` : ''}${value.hash}`;
    };

    const getCurrentUrl = () => normalizeUrl(`${window.location.pathname}${window.location.search}${window.location.hash}`);

    if (browser) {
        const currentUrl = getCurrentUrl();
        const retainedReplacementUrl = pendingPushHistoryReplacements.get(currentUrl);
        if (retainedReplacementUrl) {
            pendingPushHistoryReplacements.delete(currentUrl);
            replaceState(retainedReplacementUrl, page.state);
            searchParams = createSearchParams(window.location.search);
            Object.assign(current, parseQueryParameters(searchParams, schema, defaults));
        }
    }

    const settlePushHistoryEntry = () => {
        isCoalescingPushHistoryEntry = false;
        coalescingStartUrl = undefined;
        coalescingEntryUrl = undefined;
    };

    const flushPendingReplacement = () => {
        if (pendingReplacementUrl) {
            replaceState(pendingReplacementUrl, page.state);
            pendingReplacementUrl = undefined;
        }
    };

    const finalizePushHistoryEntry = () => {
        if (!coalescingEntryUrl || getCurrentUrl() === coalescingEntryUrl) {
            flushPendingReplacement();
            settlePushHistoryEntry();
        }
    };

    const schedulePushHistoryEntryFinalization = createDebouncedFunction(finalizePushHistoryEntry, debounceMilliseconds);

    const synchronizeURL = () => {
        if (searchParamsEqual(searchParams, window.location.search)) {
            pendingReplacementUrl = undefined;
            return;
        }

        if (history === 'push' && isCoalescingPushHistoryEntry && getCurrentUrl() !== coalescingEntryUrl) {
            // A popstate traversal may retain a pending replacement for the entry
            // we left. Editing this destination discards that Forward entry, so
            // start a fresh burst here instead of mutating the retained source.
            schedulePushHistoryEntryFinalization.cancel();
            pendingReplacementUrl = undefined;
            settlePushHistoryEntry();
        }

        const query = searchParams.toString();
        const url = `${window.location.pathname}${query ? `?${query}` : ''}${window.location.hash}`;
        if (history === 'replace') {
            replaceState(url, page.state);
        } else if (!isCoalescingPushHistoryEntry) {
            coalescingStartUrl = getCurrentUrl();
            pushState(url, page.state);
            coalescingEntryUrl = normalizeUrl(url);
            isCoalescingPushHistoryEntry = true;
        } else if (normalizeUrl(url) === coalescingStartUrl) {
            // Keep the transient state as a meaningful Back target instead of
            // replacing it with a duplicate of the entry behind it.
            schedulePushHistoryEntryFinalization.cancel();
            pendingReplacementUrl = undefined;
            pushState(url, page.state);
            settlePushHistoryEntry();
        } else {
            // Avoid exhausting browser History API mutation quotas during sustained input.
            pendingReplacementUrl = url;
        }

        if (history === 'push' && isCoalescingPushHistoryEntry) {
            schedulePushHistoryEntryFinalization();
        }
    };

    const handleBeforeNavigate = ({ type }: { type: string }) => {
        schedulePushHistoryEntryFinalization.cancel();
        if (type === 'popstate') {
            if (pendingReplacementUrl && getCurrentUrl() === coalescingEntryUrl) {
                flushPendingReplacement();
                settlePushHistoryEntry();
            }

            return;
        }

        if (getCurrentUrl() === coalescingEntryUrl) {
            flushPendingReplacement();
        } else {
            pendingReplacementUrl = undefined;
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

    onDestroy(() => {
        if (pendingReplacementUrl && coalescingEntryUrl && getCurrentUrl() !== coalescingEntryUrl) {
            pendingPushHistoryReplacements.set(coalescingEntryUrl, pendingReplacementUrl);
            pendingReplacementUrl = undefined;
            settlePushHistoryEntry();
            return;
        }

        finalizePushHistoryEntry();
    });

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
