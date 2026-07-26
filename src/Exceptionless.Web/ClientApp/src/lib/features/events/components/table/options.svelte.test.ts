import { describe, expect, it } from 'vitest';

import type { EventSummaryModel, StackSummaryModel, SummaryTemplateKeys } from '../summary';

import { defaultEventColumnVisibility, defaultStackColumnVisibility, getColumns } from './options.svelte';

describe('event table columns', () => {
    it('uses dedicated stack-mode controls instead of API sort parameters', () => {
        const result = getColumns<StackSummaryModel<SummaryTemplateKeys>>('stack_frequent');
        const columnsById = Object.fromEntries(result.map((column) => [column.id, column]));

        expect(columnsById.events?.enableSorting).toBe(false);
        expect(columnsById.first?.enableSorting).toBe(false);
        expect(columnsById.last?.enableSorting).toBe(false);
        expect(columnsById.events?.header).toBeTypeOf('function');
        expect(columnsById.first?.header).toBe('First');
        expect(columnsById.last?.header).toBeTypeOf('function');
    });

    it('keeps summary message column unsortable', () => {
        const result = getColumns<EventSummaryModel<SummaryTemplateKeys>>('summary');
        const columnsById = Object.fromEntries(result.map((column) => [column.id, column]));

        expect(columnsById.message?.enableSorting).toBe(false);
    });

    it('offers project and tags as hidden optional columns', () => {
        const columns = getColumns<EventSummaryModel<SummaryTemplateKeys>>();
        const columnIds = columns.map((column) => column.id);

        expect(columnIds).toContain('project');
        expect(columnIds).toContain('tags');
        expect(defaultEventColumnVisibility.project).toBe(false);
        expect(defaultEventColumnVisibility.tags).toBe(false);
    });

    it('offers project and tags as hidden optional stack columns', () => {
        const columns = getColumns<StackSummaryModel<SummaryTemplateKeys>>('stack_frequent');
        const columnIds = columns.map((column) => column.id);

        expect(columnIds).toContain('project');
        expect(columnIds).toContain('tags');
        expect(defaultStackColumnVisibility.project).toBe(false);
        expect(defaultStackColumnVisibility.tags).toBe(false);
    });
});
