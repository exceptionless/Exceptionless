export interface IFilter {}

export interface KeywordFilter extends IFilter {
	keyword: string;
}

export interface TermFilter extends IFilter {
	term: string;
	value: string | number | boolean | Date | null | undefined;
}

function isKeywordFilter(filter: IFilter): filter is KeywordFilter {
	return 'keyword' in filter;
}

function isTermFilter(filter: IFilter): filter is TermFilter {
	return 'term' in filter;
}

export function toFilter(filters: IFilter[]): string {
	return filters.map(toFilterPart).filter(Boolean).join(' ');
}

function toFilterPart(filter: IFilter): string | undefined {
	if (isKeywordFilter(filter)) {
		return filter.keyword;
	} else if (isTermFilter(filter)) {
		return `${filter.term}:${filter.value}`;
	}
}

/**
 * Update the filters with the given filter. If the filter already exists, it will be removed.
 * @param filters The filters
 * @param filter The filter to add or remove
 * @returns The updated filters
 */
export function updateFilters(filters: IFilter[], filter: IFilter): IFilter[] {
	const index = filters.findIndex((f) => toFilterPart(f) === toFilterPart(filter));
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
		const part = toFilterPart(filter);
		if (part) {
			// Check for whole word / phrase match
			const regex = new RegExp(`(^|\\s)${part}(\\s|$)`);
			if (regex.test(input)) {
				input = input.replace(regex, '');
				if (isKeywordFilter(filter)) {
					keywordFilterParts.push(part);
				} else {
					resolvedFilters.push(filter);
				}
			}
		}
	}

	input = `${keywordFilterParts.join(' ')} ${input ?? ''}`.trim();
	if (input) {
		resolvedFilters.push({
			keyword: input
		});
	}

	return resolvedFilters;
}
