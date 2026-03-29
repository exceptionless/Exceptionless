import type { IFilter } from '$comp/faceted-filter';
import type { PersistentEventKnownTypes } from '$features/events/models';
import type { LogLevel } from '$features/events/models/event-data';
import type { StackStatus } from '$features/stacks/models';

import { organization } from '$features/organizations/context.svelte';
import { SvelteMap } from 'svelte/reactivity';

import {
    BooleanFilter,
    DateFilter,
    KeywordFilter,
    LevelFilter,
    NumberFilter,
    ProjectFilter,
    ReferenceFilter,
    SessionFilter,
    StatusFilter,
    StringFilter,
    TagFilter,
    TypeFilter,
    VersionFilter
} from './models.svelte';

let filterCacheVersion = $state(1);
export function filterCacheVersionNumber() {
    return filterCacheVersion;
}
const filterCache = new SvelteMap<null | string, IFilter[]>();

interface SerializedFilter {
    term?: string;
    type: string;
    value?: unknown;
}

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

export function buildFilterCacheKey(organization: string | undefined, scope: string, filter: null | string): string {
    return `${organization ?? ''}:${scope}:${filter ?? ''}`;
}

export function clearFilterCache() {
    filterCache.clear();
    filterCacheVersion = 1;
}

export function deserializeFilters(json: string): IFilter[] {
    try {
        const parsed: SerializedFilter[] = JSON.parse(json);

        if (!Array.isArray(parsed)) {
            return [];
        }

        return parsed.map(reconstructFilter).filter((f): f is IFilter => f !== null);
    } catch {
        return [];
    }
}

export function filterChanged(filters: IFilter[], addedOrUpdated: IFilter): IFilter[] {
    const index = filters.findIndex((f) => f.id === addedOrUpdated.id);
    if (index === -1) {
        return processFilterRules([...filters, addedOrUpdated]);
    }

    return processFilterRules([...filters.slice(0, index), addedOrUpdated, ...filters.slice(index + 1)]);
}

export function filterRemoved(filters: IFilter[], removed?: IFilter): IFilter[] {
    // If removed is undefined, keep only date filters and remove all others.
    if (!removed) {
        return filters.filter((f) => f.type === 'date');
    }

    return filters.filter((f) => f.id !== removed.id);
}

export function getFiltersFromCache(cacheKey: string, filter: null | string): IFilter[] {
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
    if (!value) {
        return value;
    }

    const trimmed = value.trim();
    if (!trimmed) {
        return trimmed;
    }

    if (trimmed.length > 1 && trimmed.startsWith('"') && trimmed.endsWith('"')) {
        return trimmed;
    }

    // Check for lucene special characters or whitespace
    const regex = /[+\-&|!(){}[\]^"~*?:\\/\s]/;
    if (trimmed && regex.test(trimmed)) {
        return quote(trimmed);
    }

    return trimmed;
}

export function serializeFilters(filters: IFilter[]): string {
    const serialized: SerializedFilter[] = filters.map((filter) => {
        const entry: SerializedFilter = { type: filter.type };

        if ('term' in filter && (filter as { term?: string }).term !== undefined) {
            entry.term = (filter as { term?: string }).term;
        }

        if ('value' in filter) {
            entry.value = (filter as { value?: unknown }).value;
        }

        return entry;
    });

    return JSON.stringify(serialized);
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

export function updateFilterCache(cacheKey: string, filters: IFilter[]) {
    // Prevent unbounded growth
    if (filterCache.size >= 100) {
        const oldestKey = filterCache.keys().next().value;
        filterCache.delete(oldestKey as string);
    }

    filterCache.delete(cacheKey);
    filterCache.set(cacheKey, filters);
    filterCacheVersion += 1;
}

function processFilterRules(filters: IFilter[]): IFilter[] {
    const uniqueFilters = new SvelteMap<string, IFilter>();
    for (const filter of filters) {
        const singletonFilterKeys = ['date-date', 'level', 'project', 'string-stack', 'tag', 'type'];
        if (singletonFilterKeys.includes(filter.key)) {
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

function reconstructFilter(data: SerializedFilter): IFilter | null {
    switch (data.type) {
        case 'boolean':
            return new BooleanFilter(data.term, data.value as boolean | undefined);
        case 'date':
            return new DateFilter(data.term, data.value as Date | string | undefined);
        case 'keyword':
            return new KeywordFilter(data.value as string | undefined);
        case 'level':
            return new LevelFilter(data.value as LogLevel[] | undefined);
        case 'number':
            return new NumberFilter(data.term, data.value as number | undefined);
        case 'project':
            return new ProjectFilter(data.value as string[] | undefined);
        case 'reference':
            return new ReferenceFilter(data.value as string | undefined);
        case 'session':
            return new SessionFilter(data.value as string | undefined);
        case 'status':
            return new StatusFilter(data.value as StackStatus[] | undefined);
        case 'string':
            return new StringFilter(data.term, data.value as string | undefined);
        case 'tag':
            return new TagFilter(data.value as PersistentEventKnownTypes[] | undefined);
        case 'type':
            return new TypeFilter(data.value as PersistentEventKnownTypes[] | undefined);
        case 'version':
            return new VersionFilter(data.term, data.value as string | undefined);
        default:
            return null;
    }
}
