import type { ColumnOrderState, ColumnSizingState, ColumnVisibilityState } from '@tanstack/svelte-table';

import type { SavedView, SavedViewColumnSettings } from './models';

type SavedColumnState = Pick<SavedView, 'column_order' | 'column_settings' | 'columns'>;

export function buildColumnSettings(
    columnIds: string[],
    columnOrder: ColumnOrderState,
    columnVisibility: ColumnVisibilityState,
    columnSizing: ColumnSizingState
): Record<string, SavedViewColumnSettings> {
    const availableColumnIds = columnIds.filter((columnId) => columnId !== 'select');
    const availableColumnIdSet = new Set(availableColumnIds);
    const orderedColumnIds = [
        ...columnOrder.filter((columnId, index) => columnId !== 'select' && availableColumnIdSet.has(columnId) && columnOrder.indexOf(columnId) === index),
        ...availableColumnIds.filter((columnId) => !columnOrder.includes(columnId))
    ];

    return Object.fromEntries(
        orderedColumnIds.map((columnId, position) => [
            columnId,
            {
                position,
                visible: columnVisibility[columnId] ?? true,
                ...(columnSizing[columnId] !== undefined ? { width: Math.round(columnSizing[columnId]) } : {})
            }
        ])
    );
}

export function getSavedColumnOrder(view: SavedColumnState | undefined): ColumnOrderState {
    const settingsOrder = Object.entries(view?.column_settings ?? {})
        .filter((entry): entry is [string, SavedViewColumnSettings & { position: number }] => entry[1].position != null)
        .sort(([leftId, left], [rightId, right]) => left.position - right.position || leftId.localeCompare(rightId))
        .map(([columnId]) => columnId);

    return settingsOrder.length > 0 ? settingsOrder : (view?.column_order ?? []);
}

export function getSavedColumnSizing(view: Pick<SavedView, 'column_settings'> | undefined): ColumnSizingState {
    return Object.fromEntries(
        Object.entries(view?.column_settings ?? {})
            .filter((entry): entry is [string, SavedViewColumnSettings & { width: number }] => entry[1].width != null)
            .map(([columnId, settings]) => [columnId, settings.width])
    );
}

export function getSavedColumnVisibility(view: SavedColumnState | undefined): ColumnVisibilityState {
    const settingsVisibility = Object.fromEntries(
        Object.entries(view?.column_settings ?? {})
            .filter((entry): entry is [string, SavedViewColumnSettings & { visible: boolean }] => entry[1].visible != null)
            .map(([columnId, settings]) => [columnId, settings.visible])
    );

    return Object.keys(settingsVisibility).length > 0 ? settingsVisibility : (view?.columns ?? {});
}

export function savedViewColumnSizingEqual(current: ColumnSizingState | undefined, view: Pick<SavedView, 'column_settings'>): boolean {
    const saved = getSavedColumnSizing(view);
    const currentEntries = Object.entries(current ?? {}).filter(([columnId]) => columnId !== 'select');
    const savedEntries = Object.entries(saved);

    return currentEntries.length === savedEntries.length && currentEntries.every(([columnId, width]) => saved[columnId] === Math.round(width));
}
