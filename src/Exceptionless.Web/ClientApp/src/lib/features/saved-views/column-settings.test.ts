import { describe, expect, it } from 'vitest';

import type { SavedView } from './models';

import {
    buildColumnSettings,
    columnOrdersEqual,
    filterAvailableColumnIds,
    filterAvailableColumnRecord,
    getSavedAutoFillColumnId,
    getSavedAutoFillColumnSelection,
    getSavedColumnOrder,
    getSavedColumnSizing,
    getSavedColumnVisibility,
    getSavedWrappedColumnIds,
    resolveAvailableAutoFillColumnSelection,
    resolveAvailableColumnOrder,
    resolveSavedViewColumnOrder,
    savedViewColumnSizingEqual,
    savedViewColumnWrappingEqual
} from './column-settings';

describe('saved view column settings', () => {
    it('builds extensible settings from the current table state', () => {
        const result = buildColumnSettings(
            ['select', 'summary', 'project'],
            ['select', 'project', 'summary'],
            { project: true, summary: true },
            { project: 360 },
            'summary',
            undefined,
            ['summary']
        );

        expect(result).toEqual({
            project: { position: 0, visible: true, width: 360 },
            summary: { auto_fill: true, position: 1, visible: true, wrap: true }
        });
    });

    it('persists an explicit choice to keep every column fixed width', () => {
        expect(buildColumnSettings(['summary', 'date'], [], {}, { date: 480 }, null, 'summary')).toEqual({
            date: { position: 1, visible: true, width: 480 },
            summary: { auto_fill: false, position: 0, visible: true }
        });
    });

    it('distinguishes explicit None from legacy default auto-fill behavior', () => {
        const explicitNone = {
            columns: {
                summary: { auto_fill: false }
            }
        } as Pick<SavedView, 'columns'>;
        const legacyDefault = {
            columns: {
                summary: { visible: true }
            }
        } as Pick<SavedView, 'columns'>;
        const legacyFixedDefault = {
            columns: {
                summary: { visible: true, width: 480 }
            }
        } as Pick<SavedView, 'columns'>;

        expect(getSavedAutoFillColumnSelection(explicitNone, 'summary')).toBeNull();
        expect(getSavedAutoFillColumnSelection(legacyDefault, 'summary')).toBe('summary');
        expect(getSavedAutoFillColumnSelection(legacyFixedDefault, 'summary')).toBeNull();
    });

    it('reads order, visibility, width, and wrapping from structured columns', () => {
        const view = {
            columns: {
                project: { position: 0, visible: true, width: 360 },
                summary: { auto_fill: true, position: 1, visible: false, wrap: true }
            }
        } as Pick<SavedView, 'columns'>;

        expect(getSavedColumnOrder(view)).toEqual(['project', 'summary']);
        expect(getSavedColumnVisibility(view)).toEqual({ project: true, summary: false });
        expect(getSavedColumnSizing(view)).toEqual({ project: 360 });
        expect(getSavedAutoFillColumnId(view)).toBe('summary');
        expect(getSavedWrappedColumnIds(view)).toEqual(['summary']);
    });

    it('detects changed and reset column widths', () => {
        const view = {
            columns: {
                project: { width: 360 }
            }
        } as Pick<SavedView, 'columns'>;

        expect(savedViewColumnSizingEqual({ project: 360 }, view)).toBe(true);
        expect(savedViewColumnSizingEqual({ project: 359.6 }, view)).toBe(true);
        expect(savedViewColumnSizingEqual({ project: 420 }, view)).toBe(false);
        expect(savedViewColumnSizingEqual({}, view)).toBe(false);
    });

    it('compares complete resolved orders including columns added after the saved view', () => {
        const hydratedOrder = ['select', 'project', 'summary', 'new-column'];

        expect(columnOrdersEqual(['project', 'summary', 'new-column'], hydratedOrder)).toBe(true);
        expect(columnOrdersEqual(['select', 'new-column', 'project', 'summary'], hydratedOrder)).toBe(false);
    });

    it('resolves saved positions against all currently available columns', () => {
        const view = {
            columns: {
                date: { position: 1 },
                summary: { position: 0 }
            }
        } as Pick<SavedView, 'columns'>;

        expect(resolveSavedViewColumnOrder(view, ['select', 'user', 'summary', 'date'])).toEqual(['select', 'summary', 'date', 'user']);

        const savedAfterReorder = {
            columns: {
                date: { position: 2 },
                summary: { position: 1 },
                user: { position: 0 }
            }
        } as Pick<SavedView, 'columns'>;
        const currentOrder = ['select', 'user', 'summary', 'date'];
        expect(columnOrdersEqual(currentOrder, resolveSavedViewColumnOrder(savedAfterReorder, currentOrder))).toBe(true);
    });

    it('drops unavailable draft columns and includes newly available columns', () => {
        expect(resolveAvailableColumnOrder(['select', 'removed', 'summary'], ['select', 'project', 'summary', 'date'])).toEqual([
            'select',
            'summary',
            'project',
            'date'
        ]);
    });

    it('drops unavailable IDs from restored column-scoped settings', () => {
        const availableColumnIds = ['select', 'project', 'summary'];

        expect(filterAvailableColumnRecord({ removed: 480, summary: 360 }, availableColumnIds)).toEqual({ summary: 360 });
        expect(filterAvailableColumnIds(['removed', 'summary', 'summary'], availableColumnIds)).toEqual(['summary']);
        expect(resolveAvailableAutoFillColumnSelection('removed', availableColumnIds, 'summary')).toBe('summary');
        expect(resolveAvailableAutoFillColumnSelection('removed', availableColumnIds)).toBeNull();
    });

    it('treats missing wrap settings as the legacy single-line behavior', () => {
        const view = {
            columns: {
                project: { visible: true },
                summary: { visible: true }
            }
        } as Pick<SavedView, 'columns'>;

        expect(getSavedWrappedColumnIds(view)).toEqual([]);
        expect(savedViewColumnWrappingEqual([], view)).toBe(true);
        expect(savedViewColumnWrappingEqual(['summary'], view)).toBe(false);
    });

    it('compares wrapped columns without depending on their order', () => {
        const view = {
            columns: {
                project: { wrap: true },
                summary: { wrap: true }
            }
        } as Pick<SavedView, 'columns'>;

        expect(savedViewColumnWrappingEqual(['summary', 'project'], view)).toBe(true);
        expect(savedViewColumnWrappingEqual(['summary'], view)).toBe(false);
    });
});
