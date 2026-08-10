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

    const synchronizeURL = () => {
        if (searchParamsEqual(searchParams, window.location.search)) {
            return;
        }

        const query = searchParams.toString();
        const url = `?${query}${window.location.hash}`;
        if (history === 'replace') {
            replaceState(url, page.state);
        } else {
            pushState(url, page.state);
        }
    };

    const scheduleSynchronization = createDebouncedFunction(synchronizeURL, debounceMilliseconds);
    beforeNavigate(scheduleSynchronization.cancel);
    onDestroy(scheduleSynchronization.cancel);

    const commit = (result: ReturnType<typeof applyQueryParameterUpdates<T>>) => {
        searchParams = result.searchParams;
        if (result.stateChanged) {
            Object.assign(current, result.state);
        }

        if (result.urlChanged) {
            scheduleSynchronization();
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
