import type { ColumnOrderState, ColumnSizingState, ColumnVisibilityState } from '@tanstack/svelte-table';

import type { SavedView, SavedViewColumnSettings } from './models';

type SavedColumnState = Pick<SavedView, 'columns'>;

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
    const settingsOrder = Object.entries(view?.columns ?? {})
        .filter((entry): entry is [string, SavedViewColumnSettings & { position: number }] => entry[1].position != null)
        .sort(([leftId, left], [rightId, right]) => left.position - right.position || leftId.localeCompare(rightId))
        .map(([columnId]) => columnId);

    return settingsOrder;
}

export function getSavedColumnSizing(view: SavedColumnState | undefined): ColumnSizingState {
    return Object.fromEntries(
        Object.entries(view?.columns ?? {})
            .filter((entry): entry is [string, SavedViewColumnSettings & { width: number }] => entry[1].width != null)
            .map(([columnId, settings]) => [columnId, settings.width])
    );
}

export function getSavedColumnVisibility(view: SavedColumnState | undefined): ColumnVisibilityState {
    return Object.fromEntries(
        Object.entries(view?.columns ?? {})
            .filter((entry): entry is [string, SavedViewColumnSettings & { visible: boolean }] => entry[1].visible != null)
            .map(([columnId, settings]) => [columnId, settings.visible])
    );
}

export function savedViewColumnSizingEqual(current: ColumnSizingState | undefined, view: SavedColumnState): boolean {
    const saved = getSavedColumnSizing(view);
    const currentEntries = Object.entries(current ?? {}).filter(([columnId]) => columnId !== 'select');
    const savedEntries = Object.entries(saved);

    return currentEntries.length === savedEntries.length && currentEntries.every(([columnId, width]) => saved[columnId] === Math.round(width));
}
