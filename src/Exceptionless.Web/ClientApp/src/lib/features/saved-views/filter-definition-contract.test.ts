import { describe, expect, it } from 'vitest';

import { deserializeFilters, toFilter } from '#features/events/components/filters/helpers.svelte.ts';

import cases from './filter-definition-contract.json';

// SavedViewFilterDefinitions builds the same filter on the server. A view whose filter differs from its chips opens as modified.
describe('saved view filter definition contract', () => {
    it.each(cases)('$name', ({ definitions, filter }) => {
        // Arrange
        const filters = deserializeFilters(JSON.stringify(definitions)).filter((definition) => definition.type !== 'date');

        // Act
        const actual = toFilter(filters);

        // Assert
        expect(actual).toBe(filter);
    });
});
