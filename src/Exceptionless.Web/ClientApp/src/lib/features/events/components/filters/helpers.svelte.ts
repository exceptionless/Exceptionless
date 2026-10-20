import { SvelteMap } from 'svelte/reactivity';

import type { IFilter } from '#comp/faceted-filter/index.ts';
import type { LogLevel } from '#features/events/models/event-data.ts';
import type { PersistentEventKnownTypes } from '#features/events/models/index.ts';
import type { StackStatus } from '#features/stacks/models.ts';

import { organization } from '#features/organizations/context.svelte.ts';

import {
    BooleanFilter,
    DateFilter,
    EnvironmentFilter,
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
        } else {
            filters.splice(dateFilterIndex, 1);
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

/**
 * Projects that scope facet discovery: the project picker and any project terms in a raw search, such
 * as legacy saved views or hand-written URLs. Filters are combined by AND, so their scopes intersect.
 */
export function getProjectScopeFilter(filters: IFilter[]): string {
    const projectFilter = getProjectFilter(filters);
    const scopes = [
        projectFilter?.value.length ? projectFilter.value : null,
        ...filters.filter((filter) => filter.type === 'keyword').map((filter) => getSearchProjectScope((filter as KeywordFilter).value ?? ''))
    ];

    // Disjoint scopes match no events, so discovery falls back to the whole organization.
    return new ProjectFilter(intersectProjectScopes(scopes) ?? []).toFilter();
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
        const entry: SerializedFilter = {
            type: filter.type
        };

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

export function shouldRefreshPersistentEventRemoval(
    removedFromTable: boolean,
    filters: IFilter[],
    filter: null | string,
    organization_id?: string,
    project_id?: string,
    stack_id?: string,
    id?: string
) {
    return removedFromTable || shouldRefreshPersistentEventChanged(filters, filter, organization_id, project_id, stack_id, id);
}

function distinct(values: string[]): string[] {
    return values.filter((value, index) => values.indexOf(value) === index);
}

/**
 * Projects that a raw search certainly limits results to, or null when it may include any project. An
 * unrelated AND clause keeps the scope, an OR branch without one removes it, and negations never add one.
 */
function getSearchProjectScope(search: string): null | string[] {
    let position = 0;

    function readTerm(): string {
        const start = position;
        let depth = 0;
        let quoted = false;
        for (; position < search.length; position++) {
            const char = search[position]!;
            if (quoted) {
                if (char === '\\') {
                    position++;
                } else if (char === '"') {
                    quoted = false;
                }
            } else if (char === '"') {
                quoted = true;
            } else if (char === '(') {
                depth++;
            } else if (char === ')' && depth-- === 0) {
                break;
            } else if (/\s/.test(char) && depth === 0) {
                break;
            }
        }

        return search.slice(start, position);
    }

    function readGroup(): null | string[] {
        const clauses: (null | string[])[] = [];
        let isOr = false;
        let negated = false;
        while (position < search.length) {
            const char = search[position]!;
            if (char === ')') {
                position++;
                break;
            }

            if (/\s/.test(char) || char === '+') {
                position++;
                continue;
            }

            if (char === '-' || char === '!') {
                negated = true;
                position++;
                continue;
            }

            let scope: null | string[];
            if (char === '(') {
                position++;
                scope = readGroup();
            } else {
                const term = readTerm();
                if (term === 'OR' || term === '||') {
                    isOr = true;
                    continue;
                }

                if (term === 'NOT') {
                    negated = true;
                    continue;
                }

                if (term === 'AND' || term === '&&') {
                    continue;
                }

                scope = /^project(?:_id)?:/i.test(term) ? [...term.matchAll(/\b[a-f0-9]{24}\b/gi)].map(([projectId]) => projectId.toLowerCase()) : null;
            }

            clauses.push(negated ? null : scope);
            negated = false;
        }

        // Mixed AND and OR groups are treated as OR, which can only widen the scope.
        if (isOr) {
            return clauses.length > 0 && clauses.every((scope) => scope !== null) ? distinct(clauses.flat()) : null;
        }

        return intersectProjectScopes(clauses);
    }

    return readGroup();
}

/** Projects allowed by every scoped clause of an AND, or null when no clause limits the projects. */
function intersectProjectScopes(scopes: (null | string[])[]): null | string[] {
    const scoped = scopes.filter((scope) => scope !== null);
    return scoped.length > 0 ? distinct(scoped.reduce((projectIds, scope) => projectIds.filter((projectId) => scope.includes(projectId)))) : null;
}

const TYPE_FILTER_REGEX = /\btype:(\w+)\b/g;

export function hasSingleTypeFilter(filter: null | string | undefined): boolean {
    if (!filter) {
        return false;
    }

    const matches = filter.match(TYPE_FILTER_REGEX);
    return matches?.length === 1;
}

export function toFilter(filters: IFilter[]): string {
    return filters
        .map((f) => f.toFilter())
        .filter(Boolean)
        .join(' ')
        .trim();
}

export function toFilterFromSerializedFilters(json: string): null | string {
    const filter = toFilter(deserializeFilters(json).filter((f) => f.type !== 'date'));
    return filter || null;
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

function mergeDuplicateFilter(existingFilter: IFilter, filter: IFilter): void {
    existingFilter.id = filter.id;
    existingFilter.hidden = filter.hidden;

    if (!('value' in existingFilter) || !('value' in filter)) {
        return;
    }

    if (Array.isArray(existingFilter.value) && Array.isArray(filter.value)) {
        existingFilter.value = [...new Set([...existingFilter.value, ...filter.value])];
        return;
    }

    if (filter.value !== undefined) {
        existingFilter.value = filter.value;
    }
}

function processFilterRules(filters: IFilter[]): IFilter[] {
    const uniqueFilters = new SvelteMap<string, IFilter>();
    for (const filter of filters) {
        const existingFilter = uniqueFilters.get(filter.key);
        if (existingFilter) {
            mergeDuplicateFilter(existingFilter, filter);
            continue;
        }

        uniqueFilters.set(filter.key, filter);
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
        case 'environment':
            filter = new EnvironmentFilter(Array.isArray(data.value) ? data.value.filter((value): value is string => typeof value === 'string') : []);
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
