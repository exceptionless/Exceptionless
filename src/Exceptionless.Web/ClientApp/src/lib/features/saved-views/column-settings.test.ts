import { describe, expect, it } from 'vitest';

import type { SavedView } from './models';

import {
    buildColumnSettings,
    getSavedAutoFillColumnId,
    getSavedColumnOrder,
    getSavedColumnSizing,
    getSavedColumnVisibility,
    savedViewColumnOrderEqual,
    savedViewColumnSizingEqual
} from './column-settings';

describe('saved view column settings', () => {
    it('builds extensible settings from the current table state', () => {
        const result = buildColumnSettings(
            ['select', 'summary', 'project'],
            ['select', 'project', 'summary'],
            { project: true, summary: true },
            { project: 360 },
            'summary'
        );

        expect(result).toEqual({
            project: { position: 0, visible: true, width: 360 },
            summary: { auto_fill: true, position: 1, visible: true }
        });
    });

    it('does not preserve auto-fill after that column is resized or hidden', () => {
        expect(buildColumnSettings(['summary'], [], {}, { summary: 480 }, 'summary')).toEqual({
            summary: { position: 0, visible: true, width: 480 }
        });
        expect(buildColumnSettings(['summary'], [], { summary: false }, {}, 'summary')).toEqual({
            summary: { position: 0, visible: false }
        });
    });

    it('reads order, visibility, and width from structured columns', () => {
        const view = {
            columns: {
                project: { position: 0, visible: true, width: 360 },
                summary: { auto_fill: true, position: 1, visible: false }
            }
        } as Pick<SavedView, 'columns'>;

        expect(getSavedColumnOrder(view)).toEqual(['project', 'summary']);
        expect(getSavedColumnVisibility(view)).toEqual({ project: true, summary: false });
        expect(getSavedColumnSizing(view)).toEqual({ project: 360 });
        expect(getSavedAutoFillColumnId(view)).toBe('summary');
    });

    it('detects changed and reset column widths', () => {
        const view = {
            columns: {
                project: { width: 360 }
            }
        } as Pick<SavedView, 'columns'>;

        expect(savedViewColumnSizingEqual({ project: 360 }, view)).toBe(true);
        expect(savedViewColumnSizingEqual({ project: 420 }, view)).toBe(false);
        expect(savedViewColumnSizingEqual({}, view)).toBe(false);
    });

    it('compares only columns with explicitly saved positions', () => {
        const view = {
            columns: {
                date: { visible: true },
                project: { position: 0 },
                summary: { position: 1 }
            }
        } as Pick<SavedView, 'columns'>;

        expect(savedViewColumnOrderEqual(['select', 'project', 'date', 'summary', 'tags'], view)).toBe(true);
        expect(savedViewColumnOrderEqual(['select', 'summary', 'date', 'project', 'tags'], view)).toBe(false);
    });

    it('treats views without saved positions as unchanged', () => {
        const view = {
            columns: {
                project: { visible: true }
            }
        } as Pick<SavedView, 'columns'>;

        expect(savedViewColumnOrderEqual(['select', 'date', 'project'], view)).toBe(true);
    });
});
