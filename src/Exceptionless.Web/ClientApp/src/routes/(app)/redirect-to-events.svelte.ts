import type { IFilter } from '$comp/faceted-filter';

import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { buildFilterCacheKey, toFilter, updateFilterCache } from '$features/events/components/filters/helpers.svelte';

/**
 * Redirects to the events page with the given filter.
 * Primes the filter cache so the filter is preserved.
 */
export async function redirectToEventsWithFilter(organizationId: string | undefined, addedOrUpdated: IFilter): Promise<void> {
    const filter = toFilter([addedOrUpdated]);
    const filterCacheKey = buildFilterCacheKey(organizationId, '/next/', filter);
    updateFilterCache(filterCacheKey, [addedOrUpdated]);

    await goto(`${resolve('/(app)')}?filter=${encodeURIComponent(filter)}`);
}
