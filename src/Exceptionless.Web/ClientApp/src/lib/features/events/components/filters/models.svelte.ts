import type { IFilter } from '$comp/faceted-filter';
import type { PersistentEventKnownTypes } from '$features/events/models';
import type { LogLevel } from '$features/events/models/event-data';
import type { StackStatus } from '$features/stacks/models';

import { SvelteSet } from 'svelte/reactivity';

import { quoteIfSpecialCharacters } from './helpers.svelte';

export class BooleanFilter implements IFilter {
    public hidden = $state(false);
    public id: string = crypto.randomUUID();
    public term = $state<string>();
    public type: string = 'boolean';

    public value = $state<boolean>();

    public get key(): string {
        return `${this.type}-${this.term}`;
    }

    constructor(term?: string, value?: boolean) {
        this.term = term;
        this.value = value;
    }

    public clone(): IFilter {
        const filter = new BooleanFilter(this.term, this.value);
        filter.hidden = this.hidden;
        filter.id = this.id;
        return filter;
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
}

export class DateFilter implements IFilter {
    public hidden = $state(false);
    public id: string = crypto.randomUUID();
    public term = $state<string>();
    public type: string = 'date';

    public value = $state<Date | string>();

    public get key(): string {
        return `${this.type}-${this.term}`;
    }

    constructor(term?: string, value?: Date | string) {
        this.term = term;
        this.value = value;
    }

    public clone(): IFilter {
        const filter = new DateFilter(this.term, this.value);
        filter.hidden = this.hidden;
        filter.id = this.id;
        return filter;
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
}

export class EnvironmentFilter implements IFilter {
    public hidden = $state(false);
    public id: string = crypto.randomUUID();
    public readonly key = 'environment';
    public readonly type = 'environment';
    public value = $state<string[]>([]);

    constructor(value: string[] = []) {
        this.value = [...new SvelteSet(value.map((name) => name.trim().toLowerCase()))];
    }

    public clone(): EnvironmentFilter {
        const filter = new EnvironmentFilter([...this.value]);
        filter.hidden = this.hidden;
        filter.id = this.id;
        return filter;
    }

    public toFilter(): string {
        const clauses = this.value.map((value) =>
            value === '' ? '_missing_:environment' : `environment:${/^[a-z0-9][a-z0-9_.-]*$/.test(value) ? value : JSON.stringify(value)}`
        );
        return clauses.length > 1 ? `(${clauses.join(' OR ')})` : (clauses[0] ?? '');
    }
}

export class KeywordFilter implements IFilter {
    public hidden = $state(false);
    public id: string = crypto.randomUUID();
    public type: string = 'keyword';

    public value = $state<string>();

    public get key(): string {
        return this.type;
    }

    constructor(value?: string) {
        this.value = value;
    }

    public clone(): IFilter {
        const filter = new KeywordFilter(this.value);
        filter.hidden = this.hidden;
        filter.id = this.id;
        return filter;
    }

    public toFilter(): string {
        if (!this.value?.trim()) {
            return '';
        }

        return this.value!.trim();
    }
}

export class LevelFilter implements IFilter {
    public hidden = $state(false);
    public id: string = crypto.randomUUID();
    public type: string = 'level';

    public value = $state<LogLevel[]>([]);

    public get key(): string {
        return this.type;
    }

    constructor(value: LogLevel[] = []) {
        this.value = value;
    }

    public clone(): IFilter {
        const filter = new LevelFilter(this.value);
        filter.hidden = this.hidden;
        filter.id = this.id;
        return filter;
    }

    public toFilter(): string {
        if (this.value.length == 0) {
            return '';
        }

        if (this.value.length == 1) {
            return `level:${this.value[0]}`;
        }

        return `(${this.value.map((val) => `level:${val}`).join(' OR ')})`;
    }
}

export class NumberFilter implements IFilter {
    public hidden = $state(false);
    public id: string = crypto.randomUUID();
    public term = $state<string>();
    public type: string = 'number';

    public value = $state<number>();

    public get key(): string {
        return `${this.type}-${this.term}`;
    }

    constructor(term?: string, value?: number) {
        this.term = term;
        this.value = value;
    }

    public clone(): IFilter {
        const filter = new NumberFilter(this.term, this.value);
        filter.hidden = this.hidden;
        filter.id = this.id;
        return filter;
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
}

export class ProjectFilter implements IFilter {
    public hidden = $state(false);
    public id: string = crypto.randomUUID();
    public type: string = 'project';

    public value = $state<string[]>([]);

    public get key(): string {
        return this.type;
    }

    constructor(value: string[] = []) {
        this.value = value;
    }

    public clone(): IFilter {
        const filter = new ProjectFilter(this.value);
        filter.hidden = this.hidden;
        filter.id = this.id;
        return filter;
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
}

export class ReferenceFilter implements IFilter {
    public hidden = $state(false);
    public id: string = crypto.randomUUID();
    public type: string = 'reference';

    public value = $state<string>();

    public get key(): string {
        return this.type;
    }

    constructor(value?: string) {
        this.value = value;
    }

    public clone(): IFilter {
        const filter = new ReferenceFilter(this.value);
        filter.hidden = this.hidden;
        filter.id = this.id;
        return filter;
    }

    public toFilter(): string {
        if (!this.value?.trim()) {
            return '';
        }

        return `reference:${quoteIfSpecialCharacters(this.value)}`;
    }
}

export class SessionFilter implements IFilter {
    public hidden = $state(false);
    public id: string = crypto.randomUUID();
    public type: string = 'session';

    public value = $state<string>();

    public get key(): string {
        return this.type;
    }

    constructor(value?: string) {
        this.value = value;
    }

    public clone(): IFilter {
        const filter = new SessionFilter(this.value);
        filter.hidden = this.hidden;
        filter.id = this.id;
        return filter;
    }

    public toFilter(): string {
        if (!this.value?.trim()) {
            return '';
        }

        const session = quoteIfSpecialCharacters(this.value);
        return `(reference:${session} OR ref.session:${session})`;
    }
}

export class StatusFilter implements IFilter {
    public hidden = $state(false);
    public id: string = crypto.randomUUID();
    public type: string = 'status';

    public value = $state<StackStatus[]>([]);

    public get key(): string {
        return this.type;
    }

    constructor(value: StackStatus[] = []) {
        this.value = value;
    }

    public clone(): IFilter {
        const filter = new StatusFilter(this.value);
        filter.hidden = this.hidden;
        filter.id = this.id;
        return filter;
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
}

export class StringFilter implements IFilter {
    public hidden = $state(false);
    public id: string = crypto.randomUUID();
    public term = $state<string>();
    public type: string = 'string';

    public value = $state<string>();

    public get key(): string {
        return `${this.type}-${this.term}`;
    }

    constructor(term?: string, value?: string) {
        this.term = term;
        this.value = value;
    }

    public clone(): IFilter {
        const filter = new StringFilter(this.term, this.value);
        filter.hidden = this.hidden;
        filter.id = this.id;
        return filter;
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
}

export class TagFilter implements IFilter {
    public hidden = $state(false);
    public id: string = crypto.randomUUID();
    public type: string = 'tag';

    public value = $state<PersistentEventKnownTypes[]>([]);

    public get key(): string {
        return this.type;
    }

    constructor(value: PersistentEventKnownTypes[] = []) {
        this.value = value;
    }

    public clone(): IFilter {
        const filter = new TagFilter(this.value);
        filter.hidden = this.hidden;
        filter.id = this.id;
        return filter;
    }

    public toFilter(): string {
        if (this.value.length == 0) {
            return '';
        }

        if (this.value.length == 1) {
            return `tag:${quoteIfSpecialCharacters(this.value[0])}`;
        }

        return `(${this.value.map((val) => `tag:${quoteIfSpecialCharacters(val)}`).join(' OR ')})`;
    }
}

export class TypeFilter implements IFilter {
    public hidden = $state(false);
    public id: string = crypto.randomUUID();
    public type: string = 'type';

    public value = $state<PersistentEventKnownTypes[]>([]);

    public get key(): string {
        return this.type;
    }

    constructor(value: PersistentEventKnownTypes[] = []) {
        this.value = value;
    }

    public clone(): IFilter {
        const filter = new TypeFilter(this.value);
        filter.hidden = this.hidden;
        filter.id = this.id;
        return filter;
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
}

export class VersionFilter implements IFilter {
    public hidden = $state(false);
    public id: string = crypto.randomUUID();
    public term = $state<string>();
    public type: string = 'version';

    public value = $state<string>();

    public get key(): string {
        return `${this.type}-${this.term}`;
    }

    constructor(term?: string, value?: string) {
        this.term = term;
        this.value = value;
    }

    public clone(): IFilter {
        const filter = new VersionFilter(this.term, this.value);
        filter.hidden = this.hidden;
        filter.id = this.id;
        return filter;
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
}
