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

export function filterAvailableColumnIds(columnIds: readonly string[], availableColumnIds: readonly string[]): string[] {
    const availableColumnIdSet = new Set(availableColumnIds.filter((columnId) => columnId !== 'select'));
    return [...new Set(columnIds.filter((columnId) => availableColumnIdSet.has(columnId)))];
}

export function filterAvailableColumnRecord<T>(values: Record<string, T>, availableColumnIds: readonly string[]): Record<string, T> {
    const availableColumnIdSet = new Set(availableColumnIds.filter((columnId) => columnId !== 'select'));
    return Object.fromEntries(Object.entries(values).filter(([columnId]) => availableColumnIdSet.has(columnId)));
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

export function resolveAvailableAutoFillColumnSelection(
    selection: AutoFillColumnSelection,
    availableColumnIds: readonly string[],
    defaultAutoFillColumnId?: string
): AutoFillColumnSelection {
    if (selection === null) {
        return null;
    }

    const availableColumnIdSet = new Set(availableColumnIds.filter((columnId) => columnId !== 'select'));
    if (availableColumnIdSet.has(selection)) {
        return selection;
    }

    return defaultAutoFillColumnId && availableColumnIdSet.has(defaultAutoFillColumnId) ? defaultAutoFillColumnId : null;
}

export function resolveAvailableColumnOrder(preferredOrder: ColumnOrderState, availableOrder: ColumnOrderState | undefined): ColumnOrderState {
    const hasSelectionColumn = availableOrder?.includes('select') ?? false;
    const availableColumnIds = [...new Set((availableOrder ?? []).filter((columnId) => columnId !== 'select'))];
    const availableColumnIdSet = new Set(availableColumnIds);
    const resolvedPreferredOrder = preferredOrder.filter((columnId, index) => availableColumnIdSet.has(columnId) && preferredOrder.indexOf(columnId) === index);
    const resolvedOrder = [...resolvedPreferredOrder, ...availableColumnIds.filter((columnId) => !resolvedPreferredOrder.includes(columnId))];

    return hasSelectionColumn ? ['select', ...resolvedOrder] : resolvedOrder;
}

export function resolveSavedViewColumnOrder(view: SavedColumnState, availableOrder: ColumnOrderState | undefined): ColumnOrderState {
    return resolveAvailableColumnOrder(getSavedColumnOrder(view), availableOrder);
}

export function savedViewColumnSizingEqual(current: ColumnSizingState | undefined, view: SavedColumnState, availableColumnIds?: readonly string[]): boolean {
    const savedSizing = getSavedColumnSizing(view);
    const saved = availableColumnIds ? filterAvailableColumnRecord(savedSizing, availableColumnIds) : savedSizing;
    const normalizedCurrent = normalizeColumnSizing(current);
    const resolvedCurrent = availableColumnIds ? filterAvailableColumnRecord(normalizedCurrent, availableColumnIds) : normalizedCurrent;
    const currentEntries = Object.entries(resolvedCurrent);
    const savedEntries = Object.entries(saved);

    return currentEntries.length === savedEntries.length && currentEntries.every(([columnId, width]) => saved[columnId] === width);
}

export function savedViewColumnWrappingEqual(current: readonly string[] | undefined, view: SavedColumnState, availableColumnIds?: readonly string[]): boolean {
    const savedIds = getSavedWrappedColumnIds(view);
    const saved = availableColumnIds ? filterAvailableColumnIds(savedIds, availableColumnIds) : savedIds;
    const currentIds = availableColumnIds ? filterAvailableColumnIds(current ?? [], availableColumnIds) : [...new Set(current ?? [])];

    return currentIds.length === saved.length && currentIds.every((columnId) => saved.includes(columnId));
}
