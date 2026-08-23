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

    it('uses resizable summary and project columns and keeps selection fixed', () => {
        const columns = getColumns<EventSummaryModel<SummaryTemplateKeys>>();
        const project = columns.find((column) => column.id === 'project');
        const select = columns.find((column) => column.id === 'select');
        const summary = columns.find((column) => column.id === 'summary');

        expect(summary).toMatchObject({ enableResizing: true, maxSize: 1200, minSize: 240, size: 480 });
        expect(project).toMatchObject({ maxSize: 800, minSize: 160, size: 240 });
        expect(select?.enableResizing).toBe(false);
    });

    it('offers project and tags as hidden optional stack columns', () => {
        const columns = getColumns<StackSummaryModel<SummaryTemplateKeys>>(null);
        const columnIds = columns.map((column) => column.id);

        expect(columnIds).toContain('project');
        expect(columnIds).toContain('tags');
        expect(defaultStackColumnVisibility.project).toBe(false);
        expect(defaultStackColumnVisibility.tags).toBe(false);
    });
});
