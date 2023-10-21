import type { Serializer } from 'svelte-local-storage-store';

export interface IFilter {
	readonly type: string;
	toFilter(): string;
}

export class BooleanFilter implements IFilter {
	constructor(
		public term: string,
		public value?: boolean
	) {}

	public get type(): string {
		return 'boolean';
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

	public get type(): string {
		return 'date';
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
	public get type(): string {
		return 'keyword';
	}

	public toFilter(): string {
		return this.keyword;
	}
}

export class NumberFilter implements IFilter {
	constructor(
		public term: string,
		public value?: number
	) {}

	public get type(): string {
		return 'number';
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

	public get type(): string {
		return 'reference';
	}

	public toFilter(): string {
		return `reference:${quoteIfSpecialCharacters(this.referenceId)}`;
	}
}

export class SessionFilter implements IFilter {
	constructor(public sessionId: string) {}

	public get type(): string {
		return 'session';
	}

	public toFilter(): string {
		const session = quoteIfSpecialCharacters(this.sessionId);
		return `(reference:${session} OR ref.session:${session})`;
	}
}

export class StringFilter implements IFilter {
	constructor(
		public term: string,
		public value?: string | null
	) {}

	public get type(): string {
		return 'string';
	}

	public toFilter(): string {
		if (this.value === undefined) {
			return `_missing_:${this.term}`;
		}

		return `${this.term}:${quoteIfSpecialCharacters(this.value)}`;
	}
}

export class VersionFilter implements IFilter {
	constructor(
		public term: string,
		public value?: string
	) {}

	public get type(): string {
		return 'version';
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
	const regex = new RegExp(
		'\\+|\\-|\\&|\\||\\!|\\(|\\)|\\{|\\}|\\[|\\]|\\^|\\"|\\~|\\*|\\?|\\:|\\\\|\\/|\\s',
		'g'
	);

	if (value && value.match(regex)) {
		return quote(value);
	}

	return value;
}

export function quote(value?: string | null): string | undefined {
	return value ? `"${value}"` : undefined;
}

export function toFilter(filters: IFilter[]): string {
	return filters
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
export function updateFilters(filters: IFilter[], filter: IFilter): IFilter[] {
	const index = filters.findIndex((f) => f.toFilter() === filter.toFilter());
	if (index !== -1) {
		filters.splice(index, 1);
	} else {
		filters.push(filter);
	}

	return filters;
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
		input = input?.trim();
		if (!input) {
			break;
		}

		// NOTE: This is a super naive implementation...
		const part = filter.toFilter();
		if (part) {
			// Check for whole word / phrase match
			const regex = new RegExp(`(^|\\s)${part}(\\s|$)`);
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

export class FilterSerializer {
	// implements Serializer<IFilter[]> {
	public parse(text: string): IFilter[] {
		if (!text) {
			return [];
		}

		const data = JSON.parse(text);
		const filters: IFilter[] = [];
		for (const filter of data) {
			switch (filter.type) {
				case 'boolean':
					filters.push(new BooleanFilter(filter.term, filter.value));
					break;
				case 'date':
					filters.push(new DateFilter(filter.term, filter.value));
					break;
				case 'keyword':
					filters.push(new KeywordFilter(filter.keyword));
					break;
				case 'number':
					filters.push(new NumberFilter(filter.term, filter.value));
					break;
				case 'reference':
					filters.push(new ReferenceFilter(filter.referenceId));
					break;
				case 'session':
					filters.push(new SessionFilter(filter.sessionId));
					break;
				case 'string':
					filters.push(new StringFilter(filter.term, filter.value));
					break;
				case 'version':
					filters.push(new VersionFilter(filter.term, filter.value));
					break;
			}
		}

		return filters;
	}

	public stringify(object: IFilter[]): string {
		return JSON.stringify(object);
	}
}
