import { describe, expect, it } from 'vitest';

import type { EventSummaryModel, StackSummaryModel, SummaryTemplateKeys } from '../summary';

import { defaultEventColumnVisibility, defaultStackColumnVisibility, getColumns } from './options.svelte';

describe('event table columns', () => {
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
