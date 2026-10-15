import { supportsColumnWrapping } from '$features/shared/components/data-table/column-meta';
import { describe, expect, it } from 'vitest';

import type { EventSummaryModel, StackSummaryModel, SummaryTemplateKeys } from '../summary';

import { defaultEventColumnVisibility, defaultStackColumnVisibility, getColumns, getStackSortMode } from './options.svelte';

describe('event table columns', () => {
    it('accepts only supported stack sort modes', () => {
        expect(getStackSortMode('stack_frequent')).toBe('stack_frequent');
        expect(getStackSortMode('stack_recent')).toBe('stack_recent');
        expect(getStackSortMode('-events')).toBe('stack_frequent');
        expect(getStackSortMode('-last')).toBe('stack_recent');
        expect(getStackSortMode('stack_new')).toBeUndefined();
        expect(getStackSortMode('-last_occurrence')).toBeUndefined();
        expect(getStackSortMode(undefined)).toBeUndefined();
    });

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

    it('uses resizable summary and project columns and keeps selection fixed', () => {
        const columns = getColumns<EventSummaryModel<SummaryTemplateKeys>>();
        const project = columns.find((column) => column.id === 'project');
        const select = columns.find((column) => column.id === 'select');
        const summary = columns.find((column) => column.id === 'summary');

        expect(summary).toMatchObject({ enableResizing: true, maxSize: 1200, minSize: 240, size: 480 });
        expect(supportsColumnWrapping(summary?.meta)).toBe(true);
        expect(project).toMatchObject({ maxSize: 800, minSize: 160, size: 240 });
        expect(supportsColumnWrapping(project?.meta)).toBe(false);
        expect(select?.enableResizing).toBe(false);
    });

    it('offers project and tags as hidden optional stack columns', () => {
        const columns = getColumns<StackSummaryModel<SummaryTemplateKeys>>('stack_frequent');
        const columnIds = columns.map((column) => column.id);

        expect(columnIds).toContain('project');
        expect(columnIds).toContain('tags');
        expect(defaultStackColumnVisibility.project).toBe(false);
        expect(defaultStackColumnVisibility.tags).toBe(false);
    });

    it('allows wrapping only for summary, tags, and message event columns', () => {
        const columns = getColumns<EventSummaryModel<SummaryTemplateKeys>>();
        const wrappableColumnIds = columns.filter((column) => supportsColumnWrapping(column.meta)).map((column) => column.id);

        expect(wrappableColumnIds).toEqual(['summary', 'tags', 'message']);
    });

    it('allows wrapping only for summary and tags stack columns', () => {
        const columns = getColumns<StackSummaryModel<SummaryTemplateKeys>>('stack_frequent');
        const wrappableColumnIds = columns.filter((column) => supportsColumnWrapping(column.meta)).map((column) => column.id);

        expect(wrappableColumnIds).toEqual(['summary', 'tags']);
    });
});
