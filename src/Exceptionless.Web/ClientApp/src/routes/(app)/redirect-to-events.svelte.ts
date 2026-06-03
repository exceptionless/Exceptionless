import type { IFilter } from '$comp/faceted-filter';

import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { toFilter } from '$features/events/components/filters/helpers.svelte';
import { SvelteURLSearchParams } from 'svelte/reactivity';

interface ListNavigationOptions {
    time?: null | string;
}

type ListPage = 'events' | 'stacks';

export const ALL_TIME_QUERY_VALUE = 'all';

const listPagePaths = {
    events: '/(app)/event',
    stacks: '/(app)/stack'
} as const;

export function buildListPageHref(page: ListPage, _organizationId: string | undefined, filters: IFilter[], options: ListNavigationOptions = {}): string {
    const path = resolve(listPagePaths[page]);
    const filtersForNavigation = options.time === null ? filters.filter((filter) => filter.type !== 'date') : filters;
    const queryParams = new SvelteURLSearchParams();
    const rawFilters = filtersForNavigation.filter((filter) => filter.type !== 'date' && !trySetRegisteredFilterQueryParam(queryParams, filter));
    const rawFilter = toFilter(rawFilters);
    if (rawFilter) {
        queryParams.set('filter', rawFilter);
    }

    const dateFilter = filters.find((filter): filter is IFilter & { value: unknown } => filter.type === 'date' && 'value' in filter);
    const time = 'time' in options ? options.time : dateFilter?.value;
    if ('time' in options || typeof time === 'string') {
        queryParams.set('time', typeof time === 'string' ? time : ALL_TIME_QUERY_VALUE);
    }

    return `${path}?${queryParams}`;
}

/**
 * Stack filter drilldowns mean "show every event for this stack".
 * Clear any active date range so the destination cannot hide older stack events.
 */
export function getEventsNavigationOptionsForFilter(filter: IFilter): ListNavigationOptions | undefined {
    if (filter.type === 'string' && filter.key === 'string-stack') {
        return { time: null };
    }

    return undefined;
}

export async function navigateToListPage(page: ListPage, organizationId: string | undefined, filters: IFilter[], options: ListNavigationOptions = {}) {
    await goto(buildListPageHref(page, organizationId, filters, options));
}

/**
 * Redirects to the events page with the given filter.
 * Primes the filter cache so the filter is preserved.
 */
export async function redirectToEventsWithFilter(
    organizationId: string | undefined,
    addedOrUpdated: IFilter,
    options: { time?: null | string } = {}
): Promise<void> {
    await navigateToListPage('events', organizationId, [addedOrUpdated], options);
}

function trySetRegisteredFilterQueryParam(queryParams: SvelteURLSearchParams, filter: IFilter): boolean {
    if (filter.type === 'string' && filter.key === 'string-stack' && 'value' in filter && typeof filter.value === 'string' && filter.value.trim()) {
        queryParams.set('stack', filter.value);
        return true;
    }

    if (filter.type === 'project' && 'value' in filter && Array.isArray(filter.value) && filter.value.length > 0) {
        queryParams.set('project', filter.value.join(','));
        return true;
    }

    if (
        filter.type === 'boolean' &&
        'term' in filter &&
        (filter.term === 'bot' || filter.term === 'first') &&
        'value' in filter &&
        typeof filter.value === 'boolean'
    ) {
        queryParams.set(filter.term, String(filter.value));
        return true;
    }

    if (filter.type === 'level' && 'value' in filter && Array.isArray(filter.value) && filter.value.length > 0) {
        queryParams.set('level', filter.value.join(','));
        return true;
    }

    if (filter.type === 'reference' && 'value' in filter && typeof filter.value === 'string' && filter.value.trim()) {
        queryParams.set('reference', filter.value);
        return true;
    }

    if (filter.type === 'session' && 'value' in filter && typeof filter.value === 'string' && filter.value.trim()) {
        queryParams.set('session', filter.value);
        return true;
    }

    if (filter.type === 'status' && 'value' in filter && Array.isArray(filter.value) && filter.value.length > 0) {
        queryParams.set('status', filter.value.join(','));
        return true;
    }

    if (filter.type === 'tag' && 'value' in filter && Array.isArray(filter.value) && filter.value.length > 0) {
        queryParams.set('tag', filter.value.join(','));
        return true;
    }

    if (filter.type === 'type' && 'value' in filter && Array.isArray(filter.value) && filter.value.length > 0) {
        queryParams.set('type', filter.value.join(','));
        return true;
    }

    if (
        filter.type === 'version' &&
        'term' in filter &&
        filter.term === 'version' &&
        'value' in filter &&
        typeof filter.value === 'string' &&
        filter.value.trim()
    ) {
        queryParams.set('version', filter.value);
        return true;
    }

    return false;
}
