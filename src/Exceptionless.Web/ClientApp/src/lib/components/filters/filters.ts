import type { Serializer } from 'svelte-local-storage-store';

export interface IFilter {
	toFilter(): string;
}

export class BooleanFilter implements IFilter {
	constructor(
		public term: string,
		public value?: boolean
	) {}

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
		public value?: Date
	) {}

	public toFilter(): string {
		if (this.value === undefined) {
			return `_missing_:${this.term}`;
		}

		return `${this.term}:${this.value}`;
	}
}

export class KeywordFilter implements IFilter {
	constructor(public keyword: string) {}

	public toFilter(): string {
		return this.keyword;
	}
}

export class NumberFilter implements IFilter {
	constructor(
		public term: string,
		public value?: number
	) {}

	public toFilter(): string {
		if (this.value === undefined) {
			return `_missing_:${this.term}`;
		}

		return `${this.term}:${this.value}`;
	}
}

export class ReferenceFilter implements IFilter {
	constructor(public referenceId: string) {}

	public toFilter(): string {
		return `reference:${this.referenceId}`;
	}
}

export class SessionFilter implements IFilter {
	constructor(public sessionId: string) {}

	public toFilter(): string {
		return `(reference:${this.sessionId} OR ref.session:${this.sessionId})`;
	}
}

export class StringFilter implements IFilter {
	constructor(
		public term: string,
		public value?: number
	) {}

	public toFilter(): string {
		if (this.value === undefined) {
			return `_missing_:${this.term}`;
		}

		return `${this.term}:${this.value}`;
	}
}

export class VersionFilter implements IFilter {
	constructor(
		public term: string,
		public value?: string
	) {}

	public toFilter(): string {
		if (this.value === undefined) {
			return `_missing_:${this.term}`;
		}

		return `${this.term}:${this.value}`;
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


export class FilterSerializer implements Serializer<IFilter[]> {
	parse(text: string): IFilter[];
	stringify(object: IFilter[]): string;
}
