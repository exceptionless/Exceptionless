import { describe, expect, it } from 'vitest';

import type { SavedView } from './models';

import { buildColumnSettings, getSavedColumnOrder, getSavedColumnSizing, getSavedColumnVisibility, savedViewColumnSizingEqual } from './column-settings';

describe('saved view column settings', () => {
    it('builds extensible settings from the current table state', () => {
        const result = buildColumnSettings(
            ['select', 'summary', 'project'],
            ['select', 'project', 'summary'],
            { project: true, summary: true },
            { project: 360 }
        );

        expect(result).toEqual({
            project: { position: 0, visible: true, width: 360 },
            summary: { position: 1, visible: true }
        });
    });

    it('uses settings instead of legacy fields when settings are available', () => {
        const view = {
            column_order: ['summary', 'project'],
            column_settings: {
                project: { position: 0, visible: true, width: 360 },
                summary: { position: 1, visible: false }
            },
            columns: { project: false, summary: true }
        } as Pick<SavedView, 'column_order' | 'column_settings' | 'columns'>;

        expect(getSavedColumnOrder(view)).toEqual(['project', 'summary']);
        expect(getSavedColumnVisibility(view)).toEqual({ project: true, summary: false });
        expect(getSavedColumnSizing(view)).toEqual({ project: 360 });
    });

    it('falls back to legacy visibility and order', () => {
        const view = {
            column_order: ['project', 'summary'],
            column_settings: null,
            columns: { project: true, summary: false }
        } as Pick<SavedView, 'column_order' | 'column_settings' | 'columns'>;

        expect(getSavedColumnOrder(view)).toEqual(['project', 'summary']);
        expect(getSavedColumnVisibility(view)).toEqual({ project: true, summary: false });
        expect(getSavedColumnSizing(view)).toEqual({});
    });

    it('detects changed and reset column widths', () => {
        const view = {
            column_settings: {
                project: { width: 360 }
            }
        } as Pick<SavedView, 'column_settings'>;

        expect(savedViewColumnSizingEqual({ project: 360 }, view)).toBe(true);
        expect(savedViewColumnSizingEqual({ project: 420 }, view)).toBe(false);
        expect(savedViewColumnSizingEqual({}, view)).toBe(false);
    });
});
