import {
    FilterSerializer,
    toFacetedValues,
    toFilter,
    upsertOrRemoveFacetFilter,
    type IFilter,
    getFilter,
    type IFacetedFilter,
    toggleFilter,
    parseFilter,
    resetFacetedValues
} from '$comp/filters/filters';
import { persisted } from 'svelte-local-storage-store';
import { derived, get } from 'svelte/store';

export const limit = persisted<number>('events.limit', 10);
export const time = persisted<string>('filter.time', '');
const filters = persisted<IFilter[]>('filters', [], { serializer: new FilterSerializer() });

export const filter = derived(filters, ($filters) => toFilter($filters));
export const filterWithFaceted = derived(filters, ($filters) => toFilter($filters, true));
export const filterValues = derived(filters, ($filters) => toFacetedValues($filters));

export function updateFilter(filter: IFilter) {
    filters.set(toggleFilter(get(filters), filter));
}

export function updateFilterValues(key: string, values: unknown[]) {
    const filter = getFilter({ type: key, values }) as IFacetedFilter;
    const currentFilters = get(filters);
    if (filter && upsertOrRemoveFacetFilter(currentFilters, filter)) {
        filters.set(currentFilters);
    }
}

export function resetFilterValues() {
    filters.set(resetFacetedValues(get(filters)));
}

export function onFilterChanged({ detail }: CustomEvent<IFilter>): void {
    updateFilter(detail);
}

let parseFiltersDebounceTimer: ReturnType<typeof setTimeout>;
export function onFilterInputChanged(event: Event) {
    clearTimeout(parseFiltersDebounceTimer);
    parseFiltersDebounceTimer = setTimeout(() => {
        const { value } = event.target as HTMLInputElement;
        filters.set(parseFilter(get(filters), value));
    }, 250);
}

export function onFacetValuesChanged(facetKey: string, updatedValues: unknown[]) {
    updateFilterValues(facetKey, updatedValues);
}
