import type { IFilter } from '$comp/faceted-filter';

import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { serializeFilters } from '$features/events/components/filters/helpers.svelte';
import { SvelteURLSearchParams } from 'svelte/reactivity';

interface ListNavigationOptions {
    time?: null | string;
}

type ListPage = 'events' | 'stacks';

const listPagePaths = {
    events: '/(app)/event',
    stacks: '/(app)/stack'
} as const;

export function buildListPageHref(page: ListPage, _organizationId: string | undefined, filters: IFilter[], options: ListNavigationOptions = {}): string {
    const path = resolve(listPagePaths[page]);
    const filtersForNavigation = options.time === null ? filters.filter((filter) => filter.type !== 'date') : filters;
    const serializedFilters = serializeFilters(filtersForNavigation);

    const queryParams = new SvelteURLSearchParams({ filters: serializedFilters });

    const dateFilter = filters.find((filter): filter is IFilter & { value: unknown } => filter.type === 'date' && 'value' in filter);
    const time = options.time ?? dateFilter?.value;
    if ('time' in options || typeof time === 'string') {
        queryParams.set('time', typeof time === 'string' ? time : '');
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
