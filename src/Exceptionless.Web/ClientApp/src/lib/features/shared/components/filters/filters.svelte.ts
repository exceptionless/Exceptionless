import type { PersistentEventKnownTypes } from '$features/events/models';
import type { StackStatus } from '$features/stacks/models';
import type { Serializer } from '$shared/persisted.svelte';

export interface IFilter {
    isEmpty(): boolean;
    readonly key: string;
    reset(): void;
    toFilter(): string;
    readonly type: string;
}

export class BooleanFilter implements IFilter {
    public term = $state<string>();
    public type: string = 'boolean';

    public value = $state<boolean>();

    public get key(): string {
        return `${this.type}:${this.term}`;
    }

    constructor(term?: string, value?: boolean) {
        this.term = term;
        this.value = value;
    }

    public isEmpty(): boolean {
        return this.value === undefined;
    }

    public reset(): void {
        this.value = undefined;
    }

    public toFilter(): string {
        if (this.term === undefined) {
            return '';
        }

        if (this.value === undefined) {
            return `_missing_:${this.term}`;
        }

        return `${this.term}:${this.value}`;
    }

    public toJSON() {
        return {
            term: this.term,
            type: this.type,
            value: this.value
        };
    }
}

export class DateFilter implements IFilter {
    public term = $state<string>();
    public type: string = 'date';

    public value = $state<Date | string>();

    public get key(): string {
        return `${this.type}:${this.term}`;
    }

    constructor(term?: string, value?: Date | string) {
        this.term = term;
        this.value = value;
    }

    public isEmpty(): boolean {
        return this.value === undefined;
    }

    public reset(): void {
        this.value = undefined;
    }

    public toFilter(): string {
        if (this.term === undefined) {
            return '';
        }

        if (this.value === undefined) {
            return `_missing_:${this.term}`;
        }

        const date = this.value instanceof Date ? this.value.toISOString() : this.value;
        return `${this.term}:${quoteIfSpecialCharacters(date)}`;
    }

    public toJSON() {
        return {
            term: this.term,
            type: this.type,
            value: this.value
        };
    }
}

export class FilterSerializer implements Serializer<IFilter[]> {
    public deserialize(text: string): IFilter[] {
        if (!text) {
            return [];
        }

        const data: unknown[] = JSON.parse(text);
        const filters: IFilter[] = [];
        for (const filterData of data) {
            const filter = getFilter(filterData as Omit<IFilter, 'isEmpty' | 'reset' | 'toFilter'>);
            if (filter) {
                filters.push(filter);
            }
        }

        return filters;
    }

    public serialize(object: IFilter[]): string {
        return JSON.stringify(object);
    }
}

export class KeywordFilter implements IFilter {
    public type: string = 'keyword';

    public value = $state<string>();

    public get key(): string {
        return this.type;
    }

    constructor(value?: string) {
        this.value = value;
    }

    public isEmpty(): boolean {
        return !this.value?.trim();
    }

    public reset(): void {
        this.value = undefined;
    }

    public toFilter(): string {
        if (this.isEmpty()) {
            return '';
        }

        return this.value!.trim();
    }

    public toJSON() {
        return {
            type: this.type,
            value: this.value
        };
    }
}

export class NumberFilter implements IFilter {
    public term = $state<string>();
    public type: string = 'number';

    public value = $state<number>();

    public get key(): string {
        return `${this.type}:${this.term}`;
    }

    constructor(term?: string, value?: number) {
        this.term = term;
        this.value = value;
    }

    public isEmpty(): boolean {
        return this.value === undefined;
    }

    public reset(): void {
        this.value = undefined;
    }

    public toFilter(): string {
        if (this.term === undefined) {
            return '';
        }

        if (this.value === undefined) {
            return `_missing_:${this.term}`;
        }

        return `${this.term}:${this.value}`;
    }

    public toJSON() {
        return {
            term: this.term,
            type: this.type,
            value: this.value
        };
    }
}

export class OrganizationFilter implements IFilter {
    public type: string = 'organization';
    public value = $state<string>();

    public get key(): string {
        return this.type;
    }

    constructor(value?: string) {
        this.value = value;
    }

    public isEmpty(): boolean {
        return !this.value?.trim();
    }

    public reset(): void {
        this.value = undefined;
    }

    public toFilter(): string {
        if (this.isEmpty()) {
            return '';
        }

        return `organization:${this.value}`;
    }

    public toJSON() {
        return {
            type: this.type,
            value: this.value
        };
    }
}

export class ProjectFilter implements IFilter {
    public organization = $state<string | undefined>();
    public type: string = 'project';

    public value = $state<string[]>([]);

    public get key(): string {
        return this.type;
    }

    constructor(organization: string | undefined, value: string[] = []) {
        this.organization = organization;
        this.value = value;
    }

    public isEmpty(): boolean {
        return this.value.length === 0;
    }

    public reset(): void {
        this.value = [];
    }

    public toFilter(): string {
        if (this.value.length == 0) {
            return '';
        }

        if (this.value.length == 1) {
            return `project:${this.value[0]}`;
        }

        return `(${this.value.map((val) => `project:${val}`).join(' OR ')})`;
    }

    public toJSON() {
        return {
            organization: this.organization,
            type: this.type,
            value: this.value
        };
    }
}

export class ReferenceFilter implements IFilter {
    public type: string = 'reference';

    public value = $state<string>();

    public get key(): string {
        return this.type;
    }

    constructor(value?: string) {
        this.value = value;
    }

    public isEmpty(): boolean {
        return !this.value?.trim();
    }

    public reset(): void {
        this.value = undefined;
    }

    public toFilter(): string {
        if (this.isEmpty()) {
            return '';
        }

        return `reference:${quoteIfSpecialCharacters(this.value)}`;
    }

    public toJSON() {
        return {
            type: this.type,
            value: this.value
        };
    }
}

export class SessionFilter implements IFilter {
    public type: string = 'session';

    public value = $state<string>();

    public get key(): string {
        return this.type;
    }

    constructor(value?: string) {
        this.value = value;
    }

    public isEmpty(): boolean {
        return !this.value?.trim();
    }

    public reset(): void {
        this.value = undefined;
    }

    public toFilter(): string {
        if (this.isEmpty()) {
            return '';
        }

        const session = quoteIfSpecialCharacters(this.value);
        return `(reference:${session} OR ref.session:${session})`;
    }

    public toJSON() {
        return {
            type: this.type,
            value: this.value
        };
    }
}

export class StatusFilter implements IFilter {
    public type: string = 'status';

    public value = $state<StackStatus[]>([]);

    public get key(): string {
        return this.type;
    }

    constructor(value: StackStatus[] = []) {
        this.value = value;
    }

    public isEmpty(): boolean {
        return this.value.length === 0;
    }

    public reset(): void {
        this.value = [];
    }

    public toFilter(): string {
        if (this.value.length == 0) {
            return '';
        }

        if (this.value.length == 1) {
            return `status:${this.value[0]}`;
        }

        return `(${this.value.map((val) => `status:${val}`).join(' OR ')})`;
    }

    public toJSON() {
        return {
            type: this.type,
            value: this.value
        };
    }
}

export class StringFilter implements IFilter {
    public term = $state<string>();
    public type: string = 'string';

    public value = $state<string>();

    public get key(): string {
        return `${this.type}:${this.term}`;
    }

    constructor(term?: string, value?: string) {
        this.term = term;
        this.value = value;
    }

    public isEmpty(): boolean {
        return this.value === undefined;
    }

    public reset(): void {
        this.value = undefined;
    }

    public toFilter(): string {
        if (this.term === undefined) {
            return '';
        }

        if (this.value === undefined) {
            return `_missing_:${this.term}`;
        }

        return `${this.term}:${quoteIfSpecialCharacters(this.value)}`;
    }

    public toJSON() {
        return {
            term: this.term,
            type: this.type,
            value: this.value
        };
    }
}

export class TypeFilter implements IFilter {
    public type: string = 'type';

    public value = $state<PersistentEventKnownTypes[]>([]);

    public get key(): string {
        return this.type;
    }

    constructor(value: PersistentEventKnownTypes[] = []) {
        this.value = value;
    }

    public isEmpty(): boolean {
        return this.value.length === 0;
    }

    public reset(): void {
        this.value = [];
    }

    public toFilter(): string {
        if (this.value.length == 0) {
            return '';
        }

        if (this.value.length == 1) {
            return `type:${this.value[0]}`;
        }

        return `(${this.value.map((val) => `type:${val}`).join(' OR ')})`;
    }

    public toJSON() {
        return {
            type: this.type,
            value: this.value
        };
    }
}

export class VersionFilter implements IFilter {
    public term = $state<string>();
    public type: string = 'version';

    public value = $state<string>();

    public get key(): string {
        return `${this.type}:${this.term}`;
    }

    constructor(term?: string, value?: string) {
        this.term = term;
        this.value = value;
    }

    public isEmpty(): boolean {
        return this.value === undefined;
    }

    public reset(): void {
        this.value = undefined;
    }

    public toFilter(): string {
        if (this.term === undefined) {
            return '';
        }

        if (this.value === undefined) {
            return `_missing_:${this.term}`;
        }

        return `${this.term}:${quoteIfSpecialCharacters(this.value)}`;
    }

    public toJSON() {
        return {
            term: this.term,
            type: this.type,
            value: this.value
        };
    }
}

export function filterChanged(filters: IFilter[], updated: IFilter): IFilter[] {
    return processFilterRules(setFilter(filters, updated), updated);
}

export function filterRemoved(filters: IFilter[], defaultFilters: IFilter[], removed?: IFilter): IFilter[] {
    // If detail is undefined, remove all filters.
    if (!removed) {
        return defaultFilters;
    } else if (defaultFilters.find((f) => f.key === removed.key)) {
        return processFilterRules(setFilter(filters, removed), removed);
    } else {
        return processFilterRules(
            filters.filter((f) => f.key !== removed.key),
            removed
        );
    }
}

export function getDefaultFilters(includeDateFilter = true): IFilter[] {
    return [
        new OrganizationFilter(),
        new ProjectFilter(undefined, []),
        new StatusFilter([]),
        new TypeFilter([]),
        new DateFilter('date', 'last week'),
        new ReferenceFilter(),
        new SessionFilter(),
        new KeywordFilter()
    ].filter((f) => includeDateFilter || f.type !== 'date');
}

export function getFilter(filter: Omit<IFilter, 'isEmpty' | 'reset' | 'toFilter'> & Record<string, unknown>): IFilter | undefined {
    switch (filter.type) {
        case 'boolean':
            return new BooleanFilter(filter.term as string, filter.value as boolean);
        case 'date':
            return new DateFilter(filter.term as string, filter.value as Date);
        case 'keyword':
            return new KeywordFilter(filter.value as string);
        case 'number':
            return new NumberFilter(filter.term as string, filter.value as number);
        case 'organization':
            return new OrganizationFilter(filter.value as string);
        case 'project':
            return new ProjectFilter(filter.organization as string, filter.value as string[]);
        case 'reference':
            return new ReferenceFilter(filter.value as string);
        case 'session':
            return new SessionFilter(filter.value as string);
        case 'status':
            return new StatusFilter(filter.value as StackStatus[]);
        case 'string':
            return new StringFilter(filter.term as string, filter.value as string);
        case 'type':
            return new TypeFilter(filter.value as PersistentEventKnownTypes[]);
        case 'version':
            return new VersionFilter(filter.term as string, filter.value as string);
        default:
            throw new Error(`Unknown filter type: ${filter.type}`);
    }
}

export function getKeywordFilter(filters: IFilter[]): KeywordFilter | undefined {
    return filters.find((f) => f.type === 'keyword') as KeywordFilter;
}

export function getOrganizationFilter(filters: IFilter[]): OrganizationFilter | undefined {
    return filters.find((f) => f.type === 'organization') as OrganizationFilter;
}

export function getProjectFilter(filters: IFilter[]): ProjectFilter {
    return filters.find((f) => f.type === 'project') as ProjectFilter;
}

export function getStackFilter(filters: IFilter[]): StringFilter | undefined {
    return filters.find((f) => f.type === 'string') as StringFilter;
}

export function processFilterRules(filters: IFilter[], changed?: IFilter): IFilter[] {
    // Allow only one filter per type and term.
    const groupedFilters: Partial<Record<string, IFilter[]>> = Object.groupBy(filters, (f: IFilter) => f.key);
    const filtered: IFilter[] = [];
    Object.entries(groupedFilters).forEach(([, items]) => {
        if (items && items.length > 0) {
            filtered.push(items[0]);
        }
    });

    const projectFilter = filtered.find((f) => f.type === 'project') as ProjectFilter;
    if (projectFilter) {
        let organizationFilter = filtered.find((f) => f.type === 'organization') as OrganizationFilter;

        // If there is a project filter, verify the organization filter is set
        if (!organizationFilter) {
            organizationFilter = new OrganizationFilter(projectFilter.organization);
            filtered.push(organizationFilter);
        }

        // If the organization filter changes and organization is not set on the project filter, clear the project filter
        if (changed?.type === 'organization' && projectFilter.organization !== organizationFilter.value) {
            projectFilter.organization = organizationFilter.value;
            projectFilter.value = [];
        }

        // If the project filter changes and the organization filter is not set, set it
        if (organizationFilter.value !== projectFilter.organization) {
            organizationFilter.value = projectFilter.organization;
        }
    }

    return filtered;
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

/**
 * Mutates the given array of filters by adding or updating the specified filter.
 * If a filter with the same key and term already exists, it will be updated with the new filter's value.
 * If the filter does not exist, it will be added to the array.
 * @param filters - The array of filters to be mutated.
 * @param filter - The filter to be added or updated.
 * @returns The mutated array of filters.
 */
export function setFilter(filters: IFilter[], filter: IFilter): IFilter[] {
    const existingFilter = filters.find((f) => f.key === filter.key && ('term' in f && 'term' in filter ? f.term === filter.term : true));
    if (existingFilter) {
        if ('value' in existingFilter && 'value' in filter) {
            if (Array.isArray(existingFilter.value) && Array.isArray(filter.value)) {
                existingFilter.value = [...new Set([...existingFilter.value, ...filter.value])];
            } else {
                existingFilter.value = filter.value;
            }
        } else {
            Object.assign(existingFilter, filter);
        }
    } else {
        filters.push(filter);
    }

    return filters;
}

export function toFilter(filters: IFilter[]): string {
    return filters
        .map((f) => f.toFilter())
        .filter(Boolean)
        .join(' ')
        .trim();
}
