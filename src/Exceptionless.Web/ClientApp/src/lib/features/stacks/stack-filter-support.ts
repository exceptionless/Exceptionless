import type { IFilter } from '$comp/faceted-filter';

export const STACK_CRITICAL_TERM = 'critical';
const STACK_BOOLEAN_TERMS = new Set(['occurrences_are_critical', STACK_CRITICAL_TERM]);
const STACK_DATE_TERMS = new Set(['date_fixed', 'first', 'first_occurrence', 'fixedon', 'last', 'last_occurrence', 'snooze_until_utc']);
const STACK_NUMBER_TERMS = new Set(['occurrences', 'total_occurrences']);
const STACK_STRING_TERMS = new Set(['description', 'fixed_in_version', 'links', 'stack', 'title', 'type', 'version_fixed']);

export const stackFilterTerms = {
    boolean: STACK_BOOLEAN_TERMS,
    date: STACK_DATE_TERMS,
    number: STACK_NUMBER_TERMS,
    string: STACK_STRING_TERMS
} as const;

export function describeStackFilter(filter: IFilter): string {
    if ('term' in filter && typeof filter.term === 'string' && filter.term.length > 0) {
        return `${filter.type}:${filter.term}`;
    }

    return filter.type;
}

export function isStackFilterSupported(filter: IFilter): boolean {
    if (filter.type === 'keyword' || filter.type === 'project' || filter.type === 'status' || filter.type === 'tag' || filter.type === 'type') {
        return true;
    }

    if (filter.type === 'boolean') {
        return hasTerm(filter) && STACK_BOOLEAN_TERMS.has(filter.term);
    }

    if (filter.type === 'date') {
        return hasTerm(filter) && STACK_DATE_TERMS.has(filter.term);
    }

    if (filter.type === 'number') {
        return hasTerm(filter) && STACK_NUMBER_TERMS.has(filter.term);
    }

    if (filter.type === 'string') {
        return hasTerm(filter) && STACK_STRING_TERMS.has(filter.term);
    }

    return false;
}

export function splitSupportedStackFilters(filters: IFilter[]): { supported: IFilter[]; unsupported: IFilter[] } {
    const supported: IFilter[] = [];
    const unsupported: IFilter[] = [];

    for (const filter of filters) {
        if (isStackFilterSupported(filter)) {
            supported.push(filter);
        } else {
            unsupported.push(filter);
        }
    }

    return { supported, unsupported };
}

function hasTerm(filter: IFilter): filter is IFilter & { term: string } {
    return 'term' in filter && typeof filter.term === 'string' && filter.term.length > 0;
}
