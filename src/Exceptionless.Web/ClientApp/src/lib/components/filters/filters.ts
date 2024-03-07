import type { PersistentEventKnownTypes } from '$lib/models/api';
import type { StackStatus } from '$lib/models/api';
import type { Serializer } from 'svelte-persisted-store';

export interface IFilter {
    readonly type: string;
    isEmpty(): boolean;
    reset(): void;
    toFilter(): string;
}

// TODO: Consider removing.
export interface IFacetedFilter extends IFilter {
    term: string;
    values: unknown[];
    faceted: boolean;
}

export class BooleanFilter implements IFilter {
    constructor(
        public term: string,
        public value?: boolean
    ) {}

    public type: string = 'boolean';

    public isEmpty(): boolean {
        return this.value === undefined;
    }

    public reset(): void {
        this.value = undefined;
    }

    public toFilter(): string {
        if (this.value === undefined) {
            return `_missing_:${this.term}`;
        }

        return `${this.term}:${this.value}`;
    }
}

export class DateFilter implements IFilter {
    constructor(
        public term: string,
        public value?: Date | string
    ) {}

    public type: string = 'date';

    public isEmpty(): boolean {
        return this.value === undefined;
    }

    public reset(): void {
        this.value = undefined;
    }

    public toFilter(): string {
        if (this.value === undefined) {
            return `_missing_:${this.term}`;
        }

        const date = this.value instanceof Date ? this.value.toISOString() : this.value;
        return `${this.term}:${quoteIfSpecialCharacters(date)}`;
    }
}

export class KeywordFilter implements IFilter {
    constructor(public keyword: string) {}

    public type: string = 'keyword';

    public isEmpty(): boolean {
        return !this.keyword.trim();
    }

    public reset(): void {
        this.keyword = '';
    }

    public toFilter(): string {
        return this.keyword.trim();
    }
}

export class NumberFilter implements IFilter {
    constructor(
        public term: string,
        public value?: number
    ) {}

    public type: string = 'number';

    public isEmpty(): boolean {
        return this.value === undefined;
    }

    public reset(): void {
        this.value = undefined;
    }

    public toFilter(): string {
        if (this.value === undefined) {
            return `_missing_:${this.term}`;
        }

        return `${this.term}:${this.value}`;
    }
}

export class ReferenceFilter implements IFilter {
    constructor(public referenceId: string) {}

    public type: string = 'reference';

    public isEmpty(): boolean {
        return !this.referenceId.trim();
    }

    public reset(): void {
        this.referenceId = '';
    }

    public toFilter(): string {
        return `reference:${quoteIfSpecialCharacters(this.referenceId)}`;
    }
}

export class SessionFilter implements IFilter {
    constructor(public sessionId: string) {}

    public type: string = 'session';

    public isEmpty(): boolean {
        return !this.sessionId.trim();
    }

    public reset(): void {
        this.sessionId = '';
    }

    public toFilter(): string {
        const session = quoteIfSpecialCharacters(this.sessionId);
        return `(reference:${session} OR ref.session:${session})`;
    }
}

export class StatusFilter implements IFacetedFilter {
    constructor(public values: StackStatus[]) {}

    public term: string = 'status';
    public type: string = 'status';
    public faceted: boolean = true;

    public isEmpty(): boolean {
        return this.values.length === 0;
    }

    public reset(): void {
        this.values = [];
    }

    public toFilter(): string {
        if (this.values.length == 0) {
            return '';
        }

        if (this.values.length == 1) {
            return `${this.term}:${this.values[0]}`;
        }

        return `(${this.values.map((val) => `${this.term}:${val}`).join(' OR ')})`;
    }
}

export class StringFilter implements IFilter {
    constructor(
        public term: string,
        public value?: string | null
    ) {}

    public type: string = 'string';

    public isEmpty(): boolean {
        return this.value === undefined;
    }

    public reset(): void {
        this.value = undefined;
    }

    public toFilter(): string {
        if (this.value === undefined) {
            return `_missing_:${this.term}`;
        }

        return `${this.term}:${quoteIfSpecialCharacters(this.value)}`;
    }
}

export class TypeFilter implements IFacetedFilter {
    constructor(public values: PersistentEventKnownTypes[]) {}

    public term: string = 'type';
    public type: string = 'type';
    public faceted: boolean = true;

    public isEmpty(): boolean {
        return this.values.length === 0;
    }

    public reset(): void {
        this.values = [];
    }

    public toFilter(): string {
        if (this.values.length == 0) {
            return '';
        }

        if (this.values.length == 1) {
            return `${this.term}:${this.values[0]}`;
        }

        return `(${this.values.map((val) => `${this.term}:${val}`).join(' OR ')})`;
    }
}

export class VersionFilter implements IFilter {
    constructor(
        public term: string,
        public value?: string
    ) {}

    public type: string = 'version';

    public isEmpty(): boolean {
        return this.value === undefined;
    }

    public reset(): void {
        this.value = undefined;
    }

    public toFilter(): string {
        if (this.value === undefined) {
            return `_missing_:${this.term}`;
        }

        return `${this.term}:${quoteIfSpecialCharacters(this.value)}`;
    }
}

export function quoteIfSpecialCharacters(value?: string | null): string | null | undefined {
    // Check for lucene special characters or whitespace
    const regex = new RegExp('\\+|\\-|\\&|\\||\\!|\\(|\\)|\\{|\\}|\\[|\\]|\\^|\\"|\\~|\\*|\\?|\\:|\\\\|\\/|\\s', 'g');

    if (value && value.match(regex)) {
        return quote(value);
    }

    return value;
}

export function quote(value?: string | null): string | undefined {
    return value ? `"${value}"` : undefined;
}

export function toFilter(filters: IFilter[], includeFaceted: boolean = false): string {
    return filters
        .filter((f) => includeFaceted || !isFaceted(f))
        .map((f) => f.toFilter())
        .join(' ')
        .trim();
}

/**
 * Update the filters with the given filter. If the filter already exists, it will be removed.
 * @param filters The filters
 * @param filter The filter to add or remove
 * @returns The updated filters
 */
export function toggleFilter(filters: IFilter[], filter: IFilter): IFilter[] {
    const index = filters.findIndex((f) => (f.type === filter.type && isFaceted(f) && isFaceted(filter)) || f.toFilter() === filter.toFilter());

    if (index >= 0) {
        filters.splice(index, 1);
    } else {
        filters.push(filter);
    }

    return filters;
}

/**
 *  Adds or updates a given faceted filter if it has values, otherwise it will be removed
 * @param filters The filters
 * @param filter The filter to add, update or remove.
 * @returns true if filters has been modified
 */
export function upsertOrRemoveFacetFilter(filters: IFilter[], filter: IFacetedFilter): boolean {
    const index = filters.findIndex((f) => f.type === filter.type && isFaceted(f));

    // If the filter has no values, remove it.
    if (!filter.values || filter.values.length == 0) {
        if (index >= 0) {
            filters.splice(index, 1);
            return true;
        }

        return false;
    }

    if (index >= 0) {
        if (filter.toFilter() === filters[index].toFilter()) {
            return false;
        }

        filters[index] = filter;
    } else {
        filters.push(filter);
    }

    return true;
}

/**
 * Given the existing filters try and parse out any existing filters while adding new user filters as a keyword filter.
 * @param filters The current filters
 * @param filter The current filter string that was modified by the user
 * @returns The updated filter
 */
export function parseFilter(filters: IFilter[], input: string): IFilter[] {
    const resolvedFilters: IFilter[] = [];

    const keywordFilterParts = [];
    for (const filter of filters) {
        if (isFaceted(filter)) {
            resolvedFilters.push(filter);
            continue;
        }

        input = input?.trim();
        if (!input) {
            continue;
        }

        // NOTE: This is a super naive implementation...
        const part = filter.toFilter();
        if (part) {
            // Check for whole word / phrase match
            const regex = new RegExp(`(^|\\s)${part.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(\\s|$)`);
            if (regex.test(input)) {
                input = input.replace(regex, '');
                if (filter instanceof KeywordFilter) {
                    keywordFilterParts.push(part);
                } else {
                    resolvedFilters.push(filter);
                }
            }
        }
    }

    input = `${keywordFilterParts.join(' ')} ${input ?? ''}`.trim();
    if (input) {
        resolvedFilters.push(new KeywordFilter(input));
    }

    return resolvedFilters;
}

export function getFilter(filter: Omit<IFilter, 'isEmpty' | 'reset' | 'toFilter'> & Record<string, unknown>): IFilter | undefined {
    switch (filter.type) {
        case 'boolean':
            return new BooleanFilter(filter.term as string, filter.value as boolean);
        case 'date':
            return new DateFilter(filter.term as string, filter.value as Date);
        case 'keyword':
            return new KeywordFilter(filter.keyword as string);
        case 'number':
            return new NumberFilter(filter.term as string, filter.value as number);
        case 'reference':
            return new ReferenceFilter(filter.referenceId as string);
        case 'session':
            return new SessionFilter(filter.sessionId as string);
        case 'status':
            return new StatusFilter(filter.values as StackStatus[]);
        case 'string':
            return new StringFilter(filter.term as string, filter.value as string);
        case 'type':
            return new TypeFilter(filter.values as PersistentEventKnownTypes[]);
        case 'version':
            return new VersionFilter(filter.term as string, filter.value as string);
    }
}

function isFaceted(filter: IFilter): filter is IFacetedFilter {
    return 'faceted' in filter;
}

const FACETED_FILTER_TYPES = ['status', 'type'];
export function toFilterTypes(filters: IFilter[]): string[] {
    const types = new Set<string>();
    for (const filter of filters) {
        if (isFaceted(filter)) {
            types.add(filter.type);
        }
    }

    return Array.from(types);
}

export function toFacetedValues(filters: IFilter[]): Record<string, unknown[]> {
    const values: Record<string, unknown[]> = {};
    for (const filterType of FACETED_FILTER_TYPES) {
        const filter = filters.find((f) => f.type === filterType && isFaceted(f)) as IFacetedFilter | undefined;
        values[filterType] = filter?.values ?? [];
    }

    return values;
}

export function resetFacetedValues(filters: IFilter[]): IFilter[] {
    for (const filter of filters) {
        if (isFaceted(filter)) {
            upsertOrRemoveFacetFilter(filters, { ...filter, values: [] });
        }
    }

    return filters;
}

export class FilterSerializer implements Serializer<IFilter[]> {
    public parse(text: string): IFilter[] {
        if (!text) {
            return [];
        }

        const data: unknown[] = JSON.parse(text);
        const filters: IFilter[] = [];
        for (const filterData of data) {
            const filter = getFilter(filterData as Record<string, unknown>);
            if (filter) {
                filters.push(filter);
            }
        }

        return filters;
    }

    public stringify(object: IFilter[]): string {
        return JSON.stringify(object);
    }
}
