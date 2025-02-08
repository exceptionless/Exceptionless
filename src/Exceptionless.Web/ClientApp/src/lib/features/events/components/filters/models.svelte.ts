import type { IFilter } from '$comp/faceted-filter';
import type { PersistentEventKnownTypes } from '$features/events/models';
import type { StackStatus } from '$features/stacks/models';

import { quoteIfSpecialCharacters } from './helpers';

export class BooleanFilter implements IFilter {
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

export class KeywordFilter implements IFilter {
    public id: string = crypto.randomUUID();
    public type: string = 'keyword';

    public value = $state<string>();

    public get key(): string {
        return this.type;
    }

    constructor(value?: string) {
        this.value = value;
    }

    public toFilter(): string {
        if (!this.value?.trim()) {
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

export class ProjectFilter implements IFilter {
    public id: string = crypto.randomUUID();
    public type: string = 'project';

    public value = $state<string[]>([]);

    public get key(): string {
        return this.type;
    }

    constructor(value: string[] = []) {
        this.value = value;
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
            type: this.type,
            value: this.value
        };
    }
}

export class ReferenceFilter implements IFilter {
    public id: string = crypto.randomUUID();
    public type: string = 'reference';

    public value = $state<string>();

    public get key(): string {
        return this.type;
    }

    constructor(value?: string) {
        this.value = value;
    }

    public toFilter(): string {
        if (!this.value?.trim()) {
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
    public id: string = crypto.randomUUID();
    public type: string = 'session';

    public value = $state<string>();

    public get key(): string {
        return this.type;
    }

    constructor(value?: string) {
        this.value = value;
    }

    public toFilter(): string {
        if (!this.value?.trim()) {
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
    public id: string = crypto.randomUUID();
    public type: string = 'status';

    public value = $state<StackStatus[]>([]);

    public get key(): string {
        return this.type;
    }

    constructor(value: StackStatus[] = []) {
        this.value = value;
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
    public id: string = crypto.randomUUID();
    public type: string = 'type';

    public value = $state<PersistentEventKnownTypes[]>([]);

    public get key(): string {
        return this.type;
    }

    constructor(value: PersistentEventKnownTypes[] = []) {
        this.value = value;
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
