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

    it('uses a wider resizable project column and keeps utility columns fixed', () => {
        const columns = getColumns<EventSummaryModel<SummaryTemplateKeys>>();
        const project = columns.find((column) => column.id === 'project');
        const select = columns.find((column) => column.id === 'select');
        const summary = columns.find((column) => column.id === 'summary');

        expect(project).toMatchObject({ maxSize: 800, minSize: 160, size: 240 });
        expect(select?.enableResizing).toBe(false);
        expect(summary?.enableResizing).toBe(false);
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
