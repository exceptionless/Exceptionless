import type { IFilter } from '$comp/faceted-filter';

import { deserializeFilters, serializeFilters } from '$features/events/components/filters/helpers.svelte';

import type { AutoFillColumnSelection } from './column-settings';

const STORAGE_PREFIX = 'exceptionless:saved-view-draft:v1:';

export interface SavedViewDraft {
    autoFillColumnId?: AutoFillColumnSelection;
    columnOrder?: string[];
    columnSizingChanges?: Record<string, null | number>;
    columnVisibilityChanges?: Record<string, boolean | null>;
    filterChanges?: SavedViewFilterChanges;
    showChart?: boolean;
    showStats?: boolean;
    sort?: null | string;
    version: 1;
    wrappedColumnChanges?: Record<string, boolean>;
}

export interface SavedViewDraftIdentity {
    organizationId: string;
    savedViewId: string;
    userId: string;
}

export interface SavedViewFilterChanges {
    duplicateKeys?: string[];
    removedDefinitions?: string;
    removedKeys: string[];
    upsertDefinitions: string;
}

interface DraftStorage {
    getItem(key: string): null | string;
    removeItem(key: string): void;
    setItem(key: string, value: string): void;
}

export function applyFilterChanges(serverFilters: IFilter[], changes: SavedViewFilterChanges | undefined): IFilter[] {
    if (!changes) {
        return serverFilters.map((filter) => filter.clone());
    }

    const removedKeys = new Set(changes.removedKeys);
    const duplicateKeys = new Set(changes.duplicateKeys ?? []);
    const removedDefinitionCounts = buildSerializedFilterCounts(changes.removedDefinitions ? deserializeFilters(changes.removedDefinitions) : []);
    const upserts = deserializeFilters(changes.upsertDefinitions);
    const upsertsByKey = new Map(upserts.filter((filter) => !duplicateKeys.has(filter.key)).map((filter) => [filter.key, filter]));
    const duplicateUpserts = upserts.filter((filter) => duplicateKeys.has(filter.key));
    const result: IFilter[] = [];

    for (const serverFilter of serverFilters) {
        if (removedKeys.has(serverFilter.key)) {
            continue;
        }

        const serialized = serializeFilters([serverFilter]);
        const removedCount = removedDefinitionCounts.get(serialized) ?? 0;
        if (removedCount > 0) {
            removedDefinitionCounts.set(serialized, removedCount - 1);
            continue;
        }

        if (duplicateKeys.has(serverFilter.key)) {
            result.push(serverFilter.clone());
            continue;
        }

        const upsert = upsertsByKey.get(serverFilter.key);
        result.push((upsert ?? serverFilter).clone());
        upsertsByKey.delete(serverFilter.key);
    }

    result.push(...[...upsertsByKey.values()].map((filter) => filter.clone()));
    result.push(...duplicateUpserts.map((filter) => filter.clone()));
    return result;
}

export function applyRecordChanges<T>(serverValue: Record<string, T>, changes: Record<string, null | T> | undefined): Record<string, T> {
    const result = { ...serverValue };
    for (const [key, value] of Object.entries(changes ?? {})) {
        if (value === null) {
            delete result[key];
        } else {
            result[key] = value;
        }
    }

    return result;
}

export function applyWrappedColumnChanges(serverValue: string[], changes: Record<string, boolean> | undefined): string[] {
    const result = new Set(serverValue);
    for (const [columnId, isWrapped] of Object.entries(changes ?? {})) {
        if (isWrapped) {
            result.add(columnId);
        } else {
            result.delete(columnId);
        }
    }

    return [...result];
}

export function buildFilterChanges(serverFilters: IFilter[], currentFilters: IFilter[]): SavedViewFilterChanges | undefined {
    const serverByKey = groupFiltersByKey(serverFilters);
    const currentByKey = groupFiltersByKey(currentFilters);
    const duplicateKeys: string[] = [];
    const removedDefinitions: IFilter[] = [];
    const removedKeys: string[] = [];
    const upserts: IFilter[] = [];

    for (const key of new Set([...serverByKey.keys(), ...currentByKey.keys()])) {
        const server = serverByKey.get(key) ?? [];
        const current = currentByKey.get(key) ?? [];

        if (server.length <= 1 && current.length <= 1) {
            if (server.length === 1 && current.length === 0) {
                removedKeys.push(key);
            } else if (current.length === 1 && (server.length === 0 || serializeFilters(server) !== serializeFilters(current))) {
                upserts.push(current[0]!);
            }
            continue;
        }

        duplicateKeys.push(key);
        removedDefinitions.push(...getUnmatchedFilters(server, current));
        upserts.push(...getUnmatchedFilters(current, server));
    }

    if (removedKeys.length === 0 && removedDefinitions.length === 0 && upserts.length === 0) {
        return undefined;
    }

    return {
        ...(duplicateKeys.length > 0 ? { duplicateKeys } : {}),
        ...(removedDefinitions.length > 0 ? { removedDefinitions: serializeFilters(removedDefinitions) } : {}),
        removedKeys,
        upsertDefinitions: serializeFilters(upserts)
    };
}

export function buildRecordChanges<T>(serverValue: Record<string, T>, currentValue: Record<string, T>): Record<string, null | T> | undefined {
    const changes: Record<string, null | T> = {};
    const keys = new Set([...Object.keys(serverValue), ...Object.keys(currentValue)]);
    for (const key of keys) {
        if (!(key in currentValue)) {
            changes[key] = null;
        } else if (!(key in serverValue) || !Object.is(serverValue[key], currentValue[key])) {
            changes[key] = currentValue[key]!;
        }
    }

    return Object.keys(changes).length > 0 ? changes : undefined;
}

export function buildWrappedColumnChanges(serverValue: string[], currentValue: string[]): Record<string, boolean> | undefined {
    const serverIds = new Set(serverValue);
    const currentIds = new Set(currentValue);
    const changes: Record<string, boolean> = {};
    for (const columnId of new Set([...serverIds, ...currentIds])) {
        if (serverIds.has(columnId) !== currentIds.has(columnId)) {
            changes[columnId] = currentIds.has(columnId);
        }
    }

    return Object.keys(changes).length > 0 ? changes : undefined;
}

export function clearSavedViewDraft(identity: SavedViewDraftIdentity, storage: DraftStorage | undefined = getLocalStorage()): void {
    try {
        storage?.removeItem(getSavedViewDraftStorageKey(identity));
    } catch {
        // Storage can be unavailable by browser policy; the saved view still works without a local draft.
    }
}

export function getSavedViewDraft(identity: SavedViewDraftIdentity, storage: DraftStorage | undefined = getLocalStorage()): SavedViewDraft | undefined {
    try {
        const value = storage?.getItem(getSavedViewDraftStorageKey(identity));
        if (!value) {
            return undefined;
        }

        const draft: unknown = JSON.parse(value);
        return isSavedViewDraft(draft) ? draft : undefined;
    } catch {
        return undefined;
    }
}

export function getSavedViewDraftStorageKey(identity: SavedViewDraftIdentity): string {
    return `${STORAGE_PREFIX}${identity.userId}:${identity.organizationId}:${identity.savedViewId}`;
}

export function saveSavedViewDraft(identity: SavedViewDraftIdentity, draft: SavedViewDraft, storage: DraftStorage | undefined = getLocalStorage()): void {
    try {
        storage?.setItem(getSavedViewDraftStorageKey(identity), JSON.stringify(draft));
    } catch {
        // Storage can be unavailable or full; the current in-memory edits remain usable.
    }
}

function buildSerializedFilterCounts(filters: IFilter[]): Map<string, number> {
    const counts = new Map<string, number>();
    for (const filter of filters) {
        const serialized = serializeFilters([filter]);
        counts.set(serialized, (counts.get(serialized) ?? 0) + 1);
    }

    return counts;
}

function getLocalStorage(): DraftStorage | undefined {
    try {
        return typeof localStorage === 'undefined' ? undefined : localStorage;
    } catch {
        return undefined;
    }
}

function getUnmatchedFilters(filters: IFilter[], comparison: IFilter[]): IFilter[] {
    const comparisonCounts = buildSerializedFilterCounts(comparison);
    return filters.filter((filter) => {
        const serialized = serializeFilters([filter]);
        const remaining = comparisonCounts.get(serialized) ?? 0;
        if (remaining === 0) {
            return true;
        }

        comparisonCounts.set(serialized, remaining - 1);
        return false;
    });
}

function groupFiltersByKey(filters: IFilter[]): Map<string, IFilter[]> {
    const result = new Map<string, IFilter[]>();
    for (const filter of filters) {
        result.set(filter.key, [...(result.get(filter.key) ?? []), filter]);
    }

    return result;
}

function isBooleanRecord(value: unknown): value is Record<string, boolean> {
    return isRecord(value) && Object.values(value).every((item) => typeof item === 'boolean');
}

function isFilterChanges(value: unknown): value is SavedViewFilterChanges {
    return (
        isRecord(value) &&
        (value.duplicateKeys === undefined || isStringArray(value.duplicateKeys)) &&
        (value.removedDefinitions === undefined || typeof value.removedDefinitions === 'string') &&
        Array.isArray(value.removedKeys) &&
        value.removedKeys.every((item) => typeof item === 'string') &&
        typeof value.upsertDefinitions === 'string'
    );
}

function isNullableBooleanRecord(value: unknown): value is Record<string, boolean | null> {
    return isRecord(value) && Object.values(value).every((item) => item === null || typeof item === 'boolean');
}

function isNullableNumberRecord(value: unknown): value is Record<string, null | number> {
    return isRecord(value) && Object.values(value).every((item) => item === null || (typeof item === 'number' && Number.isFinite(item)));
}

function isOptionalBoolean(value: unknown): value is boolean | undefined {
    return value === undefined || typeof value === 'boolean';
}

function isOptionalNullableString(value: unknown): value is null | string | undefined {
    return value === undefined || value === null || typeof value === 'string';
}

function isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function isSavedViewDraft(value: unknown): value is SavedViewDraft {
    if (!isRecord(value) || value.version !== 1) {
        return false;
    }

    return (
        isOptionalNullableString(value.sort) &&
        isOptionalBoolean(value.showChart) &&
        isOptionalBoolean(value.showStats) &&
        (value.autoFillColumnId === undefined || value.autoFillColumnId === null || typeof value.autoFillColumnId === 'string') &&
        (value.columnOrder === undefined || isStringArray(value.columnOrder)) &&
        (value.columnSizingChanges === undefined || isNullableNumberRecord(value.columnSizingChanges)) &&
        (value.columnVisibilityChanges === undefined || isNullableBooleanRecord(value.columnVisibilityChanges)) &&
        (value.filterChanges === undefined || isFilterChanges(value.filterChanges)) &&
        (value.wrappedColumnChanges === undefined || isBooleanRecord(value.wrappedColumnChanges))
    );
}

function isStringArray(value: unknown): value is string[] {
    return Array.isArray(value) && value.every((item) => typeof item === 'string');
}
