import type { EventSummaryModel, StackSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary';

import { describe, expect, it } from 'vitest';

import { getColumns } from './options.svelte';

describe('getColumns', () => {
    it('uses dedicated stack-mode controls instead of API sort parameters', () => {
        const result = getColumns<StackSummaryModel<SummaryTemplateKeys>>('stack_frequent');
        const columnsById = Object.fromEntries(result.map((column) => [column.id, column]));

        expect(columnsById.events?.enableSorting).toBe(false);
        expect(columnsById.first?.enableSorting).toBe(false);
        expect(columnsById.last?.enableSorting).toBe(false);
        expect(columnsById.events?.header).toBeTypeOf('function');
        expect(columnsById.first?.header).toBeTypeOf('function');
        expect(columnsById.last?.header).toBeTypeOf('function');
    });

    it('keeps summary message column unsortable', () => {
        const result = getColumns<EventSummaryModel<SummaryTemplateKeys>>('summary');
        const columnsById = Object.fromEntries(result.map((column) => [column.id, column]));

        expect(columnsById.message?.enableSorting).toBe(false);
    });
});
