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

const filterCache = new SvelteMap<null | string, IFilter[]>();

interface SerializedFilter {
    hidden?: boolean;
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

        if (filter.hidden) {
            entry.hidden = true;
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

                    existingFilter.hidden = filter.hidden;
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
    let filter: IFilter | null;
    switch (data.type) {
        case 'boolean':
            filter = new BooleanFilter(data.term, data.value as boolean | undefined);
            break;
        case 'date':
            filter = new DateFilter(data.term, data.value as Date | string | undefined);
            break;
        case 'keyword':
            filter = new KeywordFilter(data.value as string | undefined);
            break;
        case 'level':
            filter = new LevelFilter(data.value as LogLevel[] | undefined);
            break;
        case 'number':
            filter = new NumberFilter(data.term, data.value as number | undefined);
            break;
        case 'project':
            filter = new ProjectFilter(data.value as string[] | undefined);
            break;
        case 'reference':
            filter = new ReferenceFilter(data.value as string | undefined);
            break;
        case 'session':
            filter = new SessionFilter(data.value as string | undefined);
            break;
        case 'status':
            filter = new StatusFilter(data.value as StackStatus[] | undefined);
            break;
        case 'string':
            filter = new StringFilter(data.term, data.value as string | undefined);
            break;
        case 'tag':
            filter = new TagFilter(data.value as PersistentEventKnownTypes[] | undefined);
            break;
        case 'type':
            filter = new TypeFilter(data.value as PersistentEventKnownTypes[] | undefined);
            break;
        case 'version':
            filter = new VersionFilter(data.term, data.value as string | undefined);
            break;
        default:
            filter = null;
    }

    if (filter) {
        filter.hidden = data.hidden === true;
    }

    return filter;
}
