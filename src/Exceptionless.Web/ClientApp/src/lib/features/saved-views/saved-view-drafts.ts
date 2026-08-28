import type { IFilter } from '$comp/faceted-filter';

import { deserializeFilters, serializeFilters } from '$features/events/components/filters/helpers.svelte';

import type { AutoFillColumnSelection } from './column-settings';

const STORAGE_PREFIX = 'exceptionless:saved-view-draft:v1:';
const MULTISET_FILTER_TYPES = new Set(['keyword']);

export interface PendingSavedViewDraftTouches {
    fields?: Array<'autoFillColumnId' | 'columnOrder' | 'showChart' | 'showStats' | 'sort'>;
    filterKeys?: string[];
    recordKeys?: Partial<Record<'columnSizingChanges' | 'columnVisibilityChanges' | 'wrappedColumnChanges', string[]>>;
}

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
    baselineDefinitions?: string;
    duplicateKeys?: string[];
    removedDefinitions?: string;
    removedKeys: string[];
    sourceDefinitions?: string;
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
    const baselineFilters = changes.baselineDefinitions ? deserializeFilters(changes.baselineDefinitions) : [];
    const upserts = deserializeFilters(changes.upsertDefinitions);
    const multisetKeys = new Set(
        [...baselineFilters, ...upserts, ...serverFilters].filter((filter) => supportsMultipleDefinitions(filter)).map((filter) => filter.key)
    );
    const rebasedKeys = new Set([
        ...baselineFilters.filter((filter) => supportsMultipleDefinitions(filter)).map((filter) => filter.key),
        ...[...duplicateKeys].filter((key) => multisetKeys.has(key))
    ]);
    const removedDefinitionCounts = buildSerializedFilterCounts(changes.removedDefinitions ? deserializeFilters(changes.removedDefinitions) : []);
    const upsertsByKey = new Map(upserts.filter((filter) => !rebasedKeys.has(filter.key)).map((filter) => [filter.key, filter]));
    const authoritativeUpsertKeys = new Set(upsertsByKey.keys());
    const rebasedUpserts = upserts.filter((filter) => rebasedKeys.has(filter.key));
    const rebasedTargetCounts =
        rebasedKeys.size === 0
            ? undefined
            : buildDuplicateTargetCounts(
                  baselineFilters,
                  changes.removedDefinitions ? deserializeFilters(changes.removedDefinitions) : [],
                  rebasedUpserts,
                  serverFilters.filter((filter) => rebasedKeys.has(filter.key))
              );
    const retainedRebasedCounts = new Map<string, number>();
    const result: IFilter[] = [];

    for (const serverFilter of serverFilters) {
        if (removedKeys.has(serverFilter.key)) {
            continue;
        }

        const serialized = serializeFilters([serverFilter]);
        if (rebasedTargetCounts && rebasedKeys.has(serverFilter.key)) {
            const retainedCount = retainedRebasedCounts.get(serialized) ?? 0;
            if (retainedCount < (rebasedTargetCounts.get(serialized) ?? 0)) {
                result.push(serverFilter.clone());
                retainedRebasedCounts.set(serialized, retainedCount + 1);
            }

            continue;
        }

        const removedCount = removedDefinitionCounts.get(serialized) ?? 0;
        if (removedCount > 0) {
            removedDefinitionCounts.set(serialized, removedCount - 1);
            continue;
        }

        if (rebasedKeys.has(serverFilter.key)) {
            result.push(serverFilter.clone());
            continue;
        }

        const upsert = upsertsByKey.get(serverFilter.key);
        if (upsert) {
            result.push(upsert.clone());
            upsertsByKey.delete(serverFilter.key);
        } else if (!authoritativeUpsertKeys.has(serverFilter.key)) {
            result.push(serverFilter.clone());
        }
    }

    result.push(...[...upsertsByKey.values()].map((filter) => filter.clone()));
    const missingRebasedUpserts = rebasedTargetCounts === undefined ? rebasedUpserts : getMissingDuplicateUpserts(rebasedUpserts, rebasedTargetCounts, result);
    result.push(...missingRebasedUpserts.map((filter) => filter.clone()));
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
    const baselineDefinitions: IFilter[] = [];
    const removedDefinitions: IFilter[] = [];
    const removedKeys: string[] = [];
    const upserts: IFilter[] = [];

    for (const key of new Set([...serverByKey.keys(), ...currentByKey.keys()])) {
        const server = serverByKey.get(key) ?? [];
        const current = currentByKey.get(key) ?? [];

        if (![...server, ...current].some((filter) => supportsMultipleDefinitions(filter))) {
            if (current.length === 0 && server.length > 0) {
                removedKeys.push(key);
            } else if (current.length > 0 && (server.length !== 1 || serializeFilters(server) !== serializeFilters(current))) {
                upserts.push(current[0]!);
            }

            continue;
        }

        if (server.length <= 1 && current.length <= 1) {
            if (server.length === 1 && current.length === 0) {
                removedDefinitions.push(server[0]!);
            } else if (current.length === 1 && server.length === 1 && serializeFilters(server) !== serializeFilters(current)) {
                baselineDefinitions.push(server[0]!);
                removedDefinitions.push(server[0]!);
                upserts.push(current[0]!);
            } else if (current.length === 1 && server.length === 0) {
                duplicateKeys.push(key);
                upserts.push(current[0]!);
            }
            continue;
        }

        duplicateKeys.push(key);
        baselineDefinitions.push(...server);
        removedDefinitions.push(...getUnmatchedFilters(server, current));
        upserts.push(...getUnmatchedFilters(current, server));
    }

    if (removedKeys.length === 0 && removedDefinitions.length === 0 && upserts.length === 0) {
        return undefined;
    }

    return {
        ...(baselineDefinitions.length > 0 ? { baselineDefinitions: serializeFilters(baselineDefinitions) } : {}),
        ...(duplicateKeys.length > 0 ? { duplicateKeys } : {}),
        ...(removedDefinitions.length > 0 ? { removedDefinitions: serializeFilters(removedDefinitions) } : {}),
        removedKeys,
        sourceDefinitions: serializeFilters(serverFilters),
        upsertDefinitions: serializeFilters(upserts)
    };
}

export function buildFilterOverrideBaselines(filters: IFilter[], overrideKeys: Iterable<string>): Record<string, string> {
    return Object.fromEntries([...overrideKeys].map((key) => [key, serializeFilters(filters.filter((filter) => filter.key === key))]));
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

export function clearSavedViewDraft(
    identity: SavedViewDraftIdentity,
    storage: DraftStorage | undefined = getSessionStorage(),
    legacyStorage: DraftStorage | undefined = getLocalStorage()
): void {
    removeSavedViewDraftFromStorage(identity, storage);
    if (legacyStorage !== storage) {
        removeSavedViewDraftFromStorage(identity, legacyStorage);
    }
}

export function getMatchingFilterOverrideKeys(filters: IFilter[], baselines: Record<string, string>): string[] {
    return Object.entries(baselines)
        .filter(([key, baseline]) => serializeFilters(filters.filter((filter) => filter.key === key)) === baseline)
        .map(([key]) => key);
}

export function getSavedViewDraft(
    identity: SavedViewDraftIdentity,
    storage: DraftStorage | undefined = getSessionStorage(),
    legacyStorage: DraftStorage | undefined = getLocalStorage()
): SavedViewDraft | undefined {
    if (legacyStorage !== storage) {
        removeSavedViewDraftFromStorage(identity, legacyStorage);
    }

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

export function mergeFilterOverrides(baseFilters: IFilter[], overrideFilters: IFilter[], overrideKeys: Iterable<string>): IFilter[] {
    const keys = new Set(overrideKeys);
    if (keys.size === 0) {
        return baseFilters.map((filter) => filter.clone());
    }

    return [
        ...baseFilters.filter((filter) => !keys.has(filter.key)).map((filter) => filter.clone()),
        ...overrideFilters.filter((filter) => keys.has(filter.key)).map((filter) => filter.clone())
    ];
}

export function mergePendingSavedViewDraftEdits(
    storedDraft: SavedViewDraft | undefined,
    pendingEdits: SavedViewDraft | undefined,
    touches?: PendingSavedViewDraftTouches,
    serverFilters?: IFilter[]
): SavedViewDraft | undefined {
    if (!pendingEdits && !touches) {
        return storedDraft;
    }

    const pending = pendingEdits ?? { version: 1 };
    const merged: SavedViewDraft = {
        ...storedDraft,
        version: 1
    };
    const mergeChanges = <T>(
        stored: Record<string, T> | undefined,
        current: Record<string, T> | undefined,
        touchedKeys: string[] | undefined
    ): Record<string, T> | undefined => {
        const result = stored || current ? { ...stored, ...current } : undefined;
        for (const key of touchedKeys ?? []) {
            if (!current || !(key in current)) {
                delete result?.[key];
            }
        }

        return result && Object.keys(result).length > 0 ? result : undefined;
    };

    merged.columnSizingChanges = mergeChanges(storedDraft?.columnSizingChanges, pending.columnSizingChanges, touches?.recordKeys?.columnSizingChanges);
    merged.columnVisibilityChanges = mergeChanges(
        storedDraft?.columnVisibilityChanges,
        pending.columnVisibilityChanges,
        touches?.recordKeys?.columnVisibilityChanges
    );
    merged.wrappedColumnChanges = mergeChanges(storedDraft?.wrappedColumnChanges, pending.wrappedColumnChanges, touches?.recordKeys?.wrappedColumnChanges);

    if (serverFilters && touches?.filterKeys?.length) {
        const storedFilters = applyFilterChanges(serverFilters, storedDraft?.filterChanges);
        const pendingFilters = applyFilterChanges(serverFilters, pending.filterChanges);
        merged.filterChanges = buildFilterChanges(serverFilters, mergeFilterOverrides(storedFilters, pendingFilters, touches.filterKeys));
    }

    for (const key of ['autoFillColumnId', 'columnOrder', 'showChart', 'showStats', 'sort'] as const) {
        if (key in pending) {
            Object.assign(merged, { [key]: pending[key] });
        } else if (touches?.fields?.includes(key)) {
            delete merged[key];
        }
    }

    return Object.entries(merged).some(([key, value]) => key !== 'version' && value !== undefined) ? merged : undefined;
}

export function saveSavedViewDraft(
    identity: SavedViewDraftIdentity,
    draft: SavedViewDraft,
    storage: DraftStorage | undefined = getSessionStorage(),
    legacyStorage: DraftStorage | undefined = getLocalStorage()
): void {
    try {
        storage?.setItem(getSavedViewDraftStorageKey(identity), JSON.stringify(draft));
    } catch {
        // Storage can be unavailable or full; the current in-memory edits remain usable.
    }

    if (legacyStorage !== storage) {
        removeSavedViewDraftFromStorage(identity, legacyStorage);
    }
}

function buildDuplicateTargetCounts(baselineFilters: IFilter[], removals: IFilter[], upserts: IFilter[], latestFilters: IFilter[]): Map<string, number> {
    const baselineCounts = buildSerializedFilterCounts(baselineFilters);
    const latestCounts = buildSerializedFilterCounts(latestFilters);
    const removalCounts = buildSerializedFilterCounts(removals);
    const upsertCounts = buildSerializedFilterCounts(upserts);
    const serializedDefinitions = new Set([...baselineCounts.keys(), ...latestCounts.keys(), ...removalCounts.keys(), ...upsertCounts.keys()]);
    const targetCounts = new Map<string, number>();

    for (const serialized of serializedDefinitions) {
        const baselineCount = baselineCounts.get(serialized) ?? 0;
        const latestCount = latestCounts.get(serialized) ?? 0;
        const localDelta = (upsertCounts.get(serialized) ?? 0) - (removalCounts.get(serialized) ?? 0);
        const adoptedDelta = localDelta > 0 ? Math.max(0, latestCount - baselineCount) : Math.max(0, baselineCount - latestCount);
        const remainingDelta = Math.sign(localDelta) * Math.max(0, Math.abs(localDelta) - adoptedDelta);
        targetCounts.set(serialized, Math.max(0, latestCount + remainingDelta));
    }

    return targetCounts;
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

function getMissingDuplicateUpserts(upserts: IFilter[], targetCounts: Map<string, number>, currentFilters: IFilter[]): IFilter[] {
    const currentCounts = buildSerializedFilterCounts(currentFilters);
    const missingCounts = new Map<string, number>();

    for (const [serialized, targetCount] of targetCounts) {
        missingCounts.set(serialized, Math.max(0, targetCount - (currentCounts.get(serialized) ?? 0)));
    }

    return upserts.filter((filter) => {
        const serialized = serializeFilters([filter]);
        const remaining = missingCounts.get(serialized) ?? 0;
        if (remaining === 0) {
            return false;
        }

        missingCounts.set(serialized, remaining - 1);
        return true;
    });
}

function getSessionStorage(): DraftStorage | undefined {
    try {
        return typeof sessionStorage === 'undefined' ? undefined : sessionStorage;
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
        (value.baselineDefinitions === undefined || typeof value.baselineDefinitions === 'string') &&
        (value.duplicateKeys === undefined || isStringArray(value.duplicateKeys)) &&
        (value.removedDefinitions === undefined || typeof value.removedDefinitions === 'string') &&
        Array.isArray(value.removedKeys) &&
        value.removedKeys.every((item) => typeof item === 'string') &&
        (value.sourceDefinitions === undefined || typeof value.sourceDefinitions === 'string') &&
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

function removeSavedViewDraftFromStorage(identity: SavedViewDraftIdentity, storage: DraftStorage | undefined): void {
    try {
        storage?.removeItem(getSavedViewDraftStorageKey(identity));
    } catch {
        // Storage can be unavailable by browser policy; the saved view still works without a stored draft.
    }
}

function supportsMultipleDefinitions(filter: IFilter): boolean {
    return MULTISET_FILTER_TYPES.has(filter.type);
}
