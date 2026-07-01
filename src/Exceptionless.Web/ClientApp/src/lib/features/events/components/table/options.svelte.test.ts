import type { EventSummaryModel, StackSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary';

import { describe, expect, it } from 'vitest';

import { getColumns } from './options.svelte';

describe('getColumns', () => {
    it('keeps stack events, first, and last columns sortable', () => {
        const result = getColumns<StackSummaryModel<SummaryTemplateKeys>>('stack_frequent');
        const columnsById = Object.fromEntries(result.map((column) => [column.id, column]));

        expect(columnsById.events?.enableSorting).toBeUndefined();
        expect(columnsById.first?.enableSorting).toBeUndefined();
        expect(columnsById.last?.enableSorting).toBeUndefined();
    });

    it('keeps summary message column unsortable', () => {
        const result = getColumns<EventSummaryModel<SummaryTemplateKeys>>('summary');
        const columnsById = Object.fromEntries(result.map((column) => [column.id, column]));

        expect(columnsById.message?.enableSorting).toBe(false);
    });
});
