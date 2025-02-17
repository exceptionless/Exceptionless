import type { IFilter } from '$comp/faceted-filter';

import { organization } from '$features/organizations/context.svelte';
import { SvelteMap } from 'svelte/reactivity';

import { DateFilter, KeywordFilter, type ProjectFilter, type StringFilter } from './models.svelte';

let filterCacheVersion = $state(1);
export function filterCacheVersionNumber() {
    return filterCacheVersion;
}
const filterCache = new SvelteMap<null | string, IFilter[]>();

export function applyTimeFilter(filters: IFilter[], time: null | string): IFilter[] {
    const dateFilterIndex = filters.findIndex((f) => f.key === 'date-date');
    if (dateFilterIndex >= 0) {
        if (time) {
            const dateFilter = filters[dateFilterIndex] as DateFilter;
            dateFilter.value = time;
        }
    } else if (time) {
        filters.push(new DateFilter('date', time));
    }

    return filters;
}

export function clearFilterCache() {
    filterCache.clear();
}

export function filterChanged(filters: IFilter[], addedOrUpdated: IFilter): IFilter[] {
    const index = filters.findIndex((f) => f.id === addedOrUpdated.id);
    if (index === -1) {
        return processFilterRules([...filters, addedOrUpdated]);
    }

    return processFilterRules([...filters.slice(0, index), addedOrUpdated, ...filters.slice(index + 1)]);
}

export function filterRemoved(filters: IFilter[], removed?: IFilter): IFilter[] {
    // If detail is undefined, remove all filters.
    if (!removed) {
        return [];
    }

    return filters.filter((f) => f.id !== removed.id);
}

export function getFiltersFromCache(filter: null | string): IFilter[] {
    const cacheKey = filter ?? '';
    if (filterCache.has(cacheKey)) {
        const filters = filterCache.get(cacheKey)?.map((f) => f.clone()) ?? [];
        return filters;
    }

    if (!filter) {
        return [];
    }

    // If filter is not in cache, return it in a new KeywordFilter instance.
    return [new KeywordFilter(filter)];
}

export function getKeywordFilter(filters: IFilter[]): KeywordFilter | undefined {
    return filters.find((f) => f.type === 'keyword') as KeywordFilter;
}

export function getProjectFilter(filters: IFilter[]): ProjectFilter {
    return filters.find((f) => f.type === 'project') as ProjectFilter;
}

export function getStackFilter(filters: IFilter[]): StringFilter | undefined {
    return filters.find((f) => f.type === 'string') as StringFilter;
}

export function quote(value?: null | string): string | undefined {
    return value ? `"${value}"` : undefined;
}

export function quoteIfSpecialCharacters(value?: null | string): null | string | undefined {
    // Check for lucene special characters or whitespace
    const regex = new RegExp('\\+|\\-|\\&|\\||\\!|\\(|\\)|\\{|\\}|\\[|\\]|\\^|\\"|\\~|\\*|\\?|\\:|\\\\|\\/|\\s', 'g');

    if (value && value.match(regex)) {
        return quote(value);
    }

    return value;
}

export function shouldRefreshPersistentEventChanged(
    filters: IFilter[],
    filter: null | string,
    organization_id?: string,
    project_id?: string,
    stack_id?: string,
    id?: string
) {
    if (!filter) {
        return true;
    }

    if (id) {
        // This could match any kind of lucene query (even must not filtering)
        const keywordFilter = getKeywordFilter(filters);
        if (keywordFilter && keywordFilter.value) {
            if (keywordFilter.value!.includes(id)) {
                return true;
            }
        }
    }

    if (stack_id) {
        const stackFilter = getStackFilter(filters);
        if (stackFilter && stackFilter.value) {
            return stackFilter.value === stack_id;
        }
    }

    if (project_id) {
        const projectFilter = getProjectFilter(filters);
        if (projectFilter && projectFilter.value.length) {
            return projectFilter.value.includes(project_id);
        }
    }

    if (organization_id) {
        return organization.current === organization_id;
    }

    return true;
}

export function toFilter(filters: IFilter[]): string {
    return filters
        .map((f) => f.toFilter())
        .filter(Boolean)
        .join(' ')
        .trim();
}

export function updateFilterCache(filter: null | string, filters: IFilter[]) {
    filterCache.set(filter ?? '', filters);
    filterCacheVersion += 1;
}

function processFilterRules(filters: IFilter[]): IFilter[] {
    // 1. There can only be one date filter by term at a time.
    // 2. There can only be one project filter.
    const uniqueFilters = new Map<string, IFilter>();
    for (const filter of filters) {
        if (filter.type === 'project' || filter.type === 'date') {
            const existingFilter = uniqueFilters.get(filter.key);
            if (existingFilter) {
                existingFilter.id = filter.id;
                if ('value' in existingFilter && 'value' in filter) {
                    if (Array.isArray(existingFilter.value) && Array.isArray(filter.value)) {
                        existingFilter.value = [...new Set([...existingFilter.value, ...filter.value])];
                    } else if (filter.value !== undefined) {
                        existingFilter.value = filter.value;
                    }
                } else {
                    throw new Error('Unable to merge filters');
                }
            }

            uniqueFilters.set(filter.key, existingFilter ?? filter);
        } else {
            uniqueFilters.set(filter.id, filter);
        }
    }

    return Array.from(uniqueFilters.values());
}
