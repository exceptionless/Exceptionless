import type { ColumnOrderState, ColumnSizingState, ColumnVisibilityState } from '@tanstack/svelte-table';

import type { SavedView, SavedViewColumnSettings } from './models';

export type AutoFillColumnSelection = null | string;
export type WrappedColumnIds = string[];

type SavedColumnState = Pick<SavedView, 'columns'>;

export function buildColumnSettings(
    columnIds: string[],
    columnOrder: ColumnOrderState,
    columnVisibility: ColumnVisibilityState,
    columnSizing: ColumnSizingState,
    autoFillColumnId?: AutoFillColumnSelection,
    defaultAutoFillColumnId?: string,
    wrappedColumnIds: readonly string[] = []
): Record<string, SavedViewColumnSettings> {
    const availableColumnIds = columnIds.filter((columnId) => columnId !== 'select');
    const availableColumnIdSet = new Set(availableColumnIds);
    const orderedColumnIds = [
        ...columnOrder.filter((columnId, index) => columnId !== 'select' && availableColumnIdSet.has(columnId) && columnOrder.indexOf(columnId) === index),
        ...availableColumnIds.filter((columnId) => !columnOrder.includes(columnId))
    ];
    const explicitNoneMarkerColumnId =
        autoFillColumnId === null ? (availableColumnIdSet.has(defaultAutoFillColumnId ?? '') ? defaultAutoFillColumnId : availableColumnIds[0]) : undefined;

    return Object.fromEntries(
        orderedColumnIds.map((columnId, position) => [
            columnId,
            {
                position,
                visible: columnVisibility[columnId] ?? true,
                ...(columnId === autoFillColumnId && columnVisibility[columnId] !== false && columnSizing[columnId] === undefined ? { auto_fill: true } : {}),
                ...(columnId === explicitNoneMarkerColumnId ? { auto_fill: false } : {}),
                ...(wrappedColumnIds.includes(columnId) ? { wrap: true } : {}),
                ...(columnSizing[columnId] !== undefined ? { width: Math.round(columnSizing[columnId]) } : {})
            }
        ])
    );
}

export function columnOrdersEqual(left: ColumnOrderState | undefined, right: ColumnOrderState | undefined): boolean {
    const normalizedLeft = (left ?? []).filter((columnId) => columnId !== 'select');
    const normalizedRight = (right ?? []).filter((columnId) => columnId !== 'select');

    return normalizedLeft.length === normalizedRight.length && normalizedLeft.every((columnId, index) => columnId === normalizedRight[index]);
}

export function getSavedAutoFillColumnId(view: SavedColumnState | undefined): string | undefined {
    return Object.entries(view?.columns ?? {}).find(([, settings]) => settings.auto_fill === true)?.[0];
}

export function getSavedAutoFillColumnSelection(view: SavedColumnState | undefined, defaultAutoFillColumnId?: string): AutoFillColumnSelection {
    const savedAutoFillColumnId = getSavedAutoFillColumnId(view);
    if (savedAutoFillColumnId) {
        return savedAutoFillColumnId;
    }

    const settings = Object.values(view?.columns ?? {});
    if (settings.some((column) => column.auto_fill === false)) {
        return null;
    }

    if (!defaultAutoFillColumnId) {
        return null;
    }

    const defaultSettings = view?.columns?.[defaultAutoFillColumnId];
    return defaultSettings?.visible === false || defaultSettings?.width != null ? null : defaultAutoFillColumnId;
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

export function getSavedWrappedColumnIds(view: SavedColumnState | undefined): WrappedColumnIds {
    return Object.entries(view?.columns ?? {})
        .filter(([, settings]) => settings.wrap === true)
        .map(([columnId]) => columnId);
}

export function normalizeColumnSizing(sizing: ColumnSizingState | undefined): ColumnSizingState {
    return Object.fromEntries(
        Object.entries(sizing ?? {})
            .filter(([columnId]) => columnId !== 'select')
            .map(([columnId, width]) => [columnId, Math.round(width)])
    );
}

export function savedViewColumnSizingEqual(current: ColumnSizingState | undefined, view: SavedColumnState): boolean {
    const saved = getSavedColumnSizing(view);
    const currentEntries = Object.entries(normalizeColumnSizing(current));
    const savedEntries = Object.entries(saved);

    return currentEntries.length === savedEntries.length && currentEntries.every(([columnId, width]) => saved[columnId] === width);
}

export function savedViewColumnWrappingEqual(current: readonly string[] | undefined, view: SavedColumnState): boolean {
    const saved = getSavedWrappedColumnIds(view);
    const currentIds = [...new Set(current ?? [])];

    return currentIds.length === saved.length && currentIds.every((columnId) => saved.includes(columnId));
}
