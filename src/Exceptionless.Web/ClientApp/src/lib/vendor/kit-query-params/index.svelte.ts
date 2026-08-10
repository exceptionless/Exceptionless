import { building } from '$app/environment';
import { afterNavigate, beforeNavigate, goto } from '$app/navigation';
import { page } from '$app/state';
import { onDestroy } from 'svelte';

import type { QueryParamsOptions, Schema, SchemaOutput } from './types.js';

import { createProxy } from './proxy.js';
import { applyQueryParamUpdates, debounce, parseURL, resetQueryParams } from './utils.js';

export function queryParamsState<T extends Schema>({ debounce: debounceTime = 200, default: defaults, pushHistory = false, schema }: QueryParamsOptions<T>) {
    const initialURL = building ? new URL('https://localhost') : page.url;
    let searchParams = new URLSearchParams(initialURL.search);
    const current = $state<SchemaOutput<T>>(parseURL(searchParams, schema, defaults));

    const synchronizeURL = () => {
        const query = searchParams.toString();
        const currentQuery = new URLSearchParams(window.location.search).toString();
        if (query === currentQuery) {
            return;
        }

        goto(`?${query}${window.location.hash}`, {
            keepFocus: true,
            noScroll: true,
            replaceState: !pushHistory
        });
    };

    const scheduleSynchronization = debounce(synchronizeURL, debounceTime);
    beforeNavigate(scheduleSynchronization.cancel);
    onDestroy(scheduleSynchronization.cancel);

    const commit = (result: ReturnType<typeof applyQueryParamUpdates<T>>) => {
        searchParams = result.searchParams;
        if (result.stateChanged) {
            Object.assign(current, result.state);
        }

        if (result.urlChanged) {
            scheduleSynchronization();
        }
    };

    const update = (values: Parameters<typeof applyQueryParamUpdates<T>>[2]) => {
        commit(applyQueryParamUpdates(current, searchParams, values, schema));
    };

    const reset = () => {
        commit(resetQueryParams(current, searchParams, schema, defaults));
    };

    afterNavigate(({ to }) => {
        if (!to) {
            return;
        }

        const nextSearchParams = new URLSearchParams(to.url.search);
        if (nextSearchParams.toString() === searchParams.toString()) {
            return;
        }

        searchParams = nextSearchParams;
        const nextState = parseURL(searchParams, schema, defaults);
        Object.assign(current, nextState);
    });

    return createProxy(current, schema, {
        reset,
        toURLSearchParams: () => new URLSearchParams(searchParams),
        update
    });
}
