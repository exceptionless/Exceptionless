import type { IFilter } from '$comp/faceted-filter';
import type { ColumnOrderState, ColumnSizingState, ColumnVisibilityState } from '@tanstack/svelte-table';

import { afterNavigate, goto } from '$app/navigation';
import { page } from '$app/state';
import {
    applyTimeFilter,
    buildFilterCacheKey,
    deserializeFilters,
    getFiltersFromCache,
    serializeFilters
} from '$features/events/components/filters/helpers.svelte';
import { organization } from '$features/organizations/context.svelte';
import { getMeQuery } from '$features/users/api.svelte';
import { tick, untrack } from 'svelte';

import type { AutoFillColumnSelection, WrappedColumnIds } from './column-settings';
import type { SavedView } from './models';

import { getSavedViewsByViewQuery } from './api.svelte';
import {
    columnOrdersEqual,
    filterAvailableColumnIds,
    filterAvailableColumnRecord,
    getSavedAutoFillColumnSelection,
    getSavedColumnOrder,
    getSavedColumnSizing,
    getSavedColumnVisibility,
    getSavedWrappedColumnIds,
    normalizeColumnSizing,
    resolveAvailableAutoFillColumnSelection,
    resolveAvailableColumnOrder,
    resolveSavedViewColumnOrder,
    savedViewColumnSizingEqual,
    savedViewColumnWrappingEqual
} from './column-settings';
import {
    applyFilterChanges,
    applyRecordChanges,
    applyWrappedColumnChanges,
    buildFilterChanges,
    buildFilterOverrideBaselines,
    buildRecordChanges,
    buildWrappedColumnChanges,
    clearSavedViewDraft,
    getMatchingFilterOverrideKeys,
    getSavedViewDraft,
    mergeFilterOverrides,
    mergePendingSavedViewDraftEdits,
    type SavedViewDraft,
    type SavedViewDraftIdentity,
    saveSavedViewDraft
} from './saved-view-drafts';
import { savedViewHref, savedViewResolvedSlug } from './slugs';

export interface SavedViewQueryParams {
    filter: null | string | undefined;
    filters?: null | string | undefined;
    saved?: null | string | undefined;
    sort?: null | string;
    time?: null | string;
    update?: (values: Partial<SavedViewQueryParams>, options?: { history?: 'push' | 'replace' }) => void;
}

export interface UseSavedViewsOptions {
    applyFilters?: (filters: IFilter[], options?: { history?: 'push' | 'replace' }) => void;
    baseHref?: string;
    defaultAutoFillColumnId?: string;
    defaultColumnVisibility?: ColumnVisibilityState;
    defaultFilter?: null | string;
    defaultTime?: null | string;
    filterCacheKey: (filter: null | string) => string;
    getAvailableColumnIds?: () => string[];
    getColumnOrder?: () => ColumnOrderState;
    getColumnSizing?: () => ColumnSizingState;
    getColumnVisibility?: () => ColumnVisibilityState;
    getFilter?: () => null | string;
    getFilterDefinitions?: () => string;
    getShowChart?: () => boolean;
    getShowStats?: () => boolean;
    getSort?: () => null | string | undefined;
    getTime?: () => null | string | undefined;
    queryParams: SavedViewQueryParams;
    setColumnOrder?: (order: ColumnOrderState) => void;
    setColumnSizing?: (sizing: ColumnSizingState) => void;
    setColumnVisibility?: (visibility: ColumnVisibilityState) => void;
    setShowChart?: (show: boolean) => void;
    setShowStats?: (show: boolean) => void;
    slug?: string;
    updateFilterCache: (key: string, filters: IFilter[]) => void;
    view: string;
}

export interface UseSavedViewsReturn {
    activeSavedView: SavedView | undefined;
    autoFillColumnId: AutoFillColumnSelection;
    canModifySavedView: boolean;
    handleClearSavedView: () => void;
    handleLoadView: (view: SavedView) => void;
    handleResetToSaved: () => void;
    handleSavedViewUpdated: (view: SavedView) => void;
    hydratedSavedViewId: string | undefined;
    isEnabled: boolean;
    isError: boolean;
    isLoading: boolean;
    isMissing: boolean;
    isModified: boolean;
    savedViews: SavedView[];
    setAutoFillColumnId: (columnId: AutoFillColumnSelection) => void;
    setWrappedColumnIds: (columnIds: WrappedColumnIds) => void;
    wrappedColumnIds: WrappedColumnIds;
}

const SAVED_VIEW_QUERY_PARAMETER_FILTER_KEYS: readonly string[] = [
    'boolean-bot',
    'boolean-first',
    'level',
    'project',
    'reference',
    'session',
    'string-stack',
    'status',
    'tag',
    'type',
    'version-version'
];

export function clearSavedViewQueryParams(queryParams: SavedViewQueryParams): void {
    queryParams.filter = null;

    if (supportsFiltersQueryParam(queryParams)) {
        queryParams.filters = null;
    }

    setSortQueryParam(queryParams, null);
    setTimeQueryParam(queryParams, null);

    if (supportsSavedQueryParam(queryParams)) {
        queryParams.saved = null;
    }
}

export function filterDefinitionsEqual(a: null | string | undefined, b: null | string | undefined): boolean {
    return normalizeFilterDefinitions(a) === normalizeFilterDefinitions(b);
}

export function getComparableSavedViewFilter(
    filter: null | string | undefined,
    filterDefinitions: null | string | undefined,
    defaultFilter: null | string | undefined
): null | string {
    if (filter != null) {
        return filter || null;
    }

    return filterDefinitions ? null : (defaultFilter ?? null);
}

export function getComparableSavedViewTime(time: null | string | undefined, defaultTime: null | string | undefined): null | string {
    return time ?? defaultTime ?? null;
}

export function getDraftSortQueryParam(serverSort: null | string, draftSort: null | string): null | string {
    if (draftSort === serverSort) {
        return null;
    }

    return draftSort ?? '';
}

export function getDraftSortValue(
    serverSort: null | string,
    currentSort: null | string,
    initialOverride: undefined | { value: null | string },
    storedDraft: SavedViewDraft | undefined
): null | string {
    if (initialOverride?.value === currentSort) {
        return storedDraft && 'sort' in storedDraft ? (storedDraft.sort ?? null) : serverSort;
    }

    return currentSort;
}

export function getEmptyFilterOverrideKeys(serverFilters: IFilter[], currentFilters: IFilter[], draft: SavedViewDraft | undefined): string[] {
    const draftFilters = applyFilterChanges(serverFilters, draft?.filterChanges);
    const keys = [...serverFilters, ...currentFilters, ...draftFilters]
        .filter((filter) => filter.type !== 'date' && !SAVED_VIEW_QUERY_PARAMETER_FILTER_KEYS.includes(filter.key))
        .map((filter) => filter.key);

    return keys.filter((key, index) => keys.indexOf(key) === index);
}

export function getSavedViewStateSignature(
    view: Pick<SavedView, 'columns' | 'filter' | 'filter_definitions' | 'show_chart' | 'show_stats' | 'sort' | 'time'>
): string {
    const columns = Object.entries(view.columns ?? {})
        .sort(([left], [right]) => left.localeCompare(right))
        .map(([columnId, settings]) => [
            columnId,
            settings.auto_fill ?? null,
            settings.position ?? null,
            settings.visible ?? null,
            settings.width ?? null,
            settings.wrap ?? null
        ]);

    return JSON.stringify({
        columns,
        filter: view.filter ?? null,
        filterDefinitions: view.filter_definitions ?? null,
        showChart: view.show_chart ?? true,
        showStats: view.show_stats ?? true,
        sort: view.sort ?? null,
        time: view.time ?? null
    });
}

const SAVED_VIEW_OVERRIDE_QUERY_PARAMETERS = [
    'bot',
    'filter',
    'filters',
    'first',
    'level',
    'project',
    'reference',
    'session',
    'sort',
    'stack',
    'status',
    'tag',
    'time',
    'type',
    'version'
] as const;

export function getSavedViewOverrideSignature(url: URL): string {
    return JSON.stringify(SAVED_VIEW_OVERRIDE_QUERY_PARAMETERS.map((parameter) => [parameter, url.searchParams.getAll(parameter)]));
}

export function hasMissingSavedView(options: {
    activeSavedView: SavedView | undefined;
    isLoading: boolean;
    savedViewKey: null | string | undefined;
    savedViews: SavedView[] | undefined;
}): boolean {
    return !!options.savedViewKey && !options.activeSavedView && !!options.savedViews && !options.isLoading;
}

export function hasSavedViewAutoFillChange(
    current: AutoFillColumnSelection,
    view: Pick<SavedView, 'columns'>,
    defaultAutoFillColumnId?: string,
    availableColumnIds?: readonly string[]
): boolean {
    const savedSelection = getSavedAutoFillColumnSelection(view, defaultAutoFillColumnId);
    const resolvedSavedSelection = availableColumnIds
        ? resolveAvailableAutoFillColumnSelection(savedSelection, availableColumnIds, defaultAutoFillColumnId)
        : savedSelection;
    return current !== resolvedSavedSelection;
}

export function hasSavedViewColumnChanges(
    current: ColumnVisibilityState | undefined,
    saved: null | Record<string, boolean> | undefined,
    defaultColumnVisibility: ColumnVisibilityState = {}
): boolean {
    return !savedViewColumnsEqual(current, saved, defaultColumnVisibility);
}

export function isSavedViewHydrationPending(
    savedViewKey: null | string | undefined,
    activeSavedViewId: string | undefined,
    hydratedSavedViewId: string | undefined,
    isUnavailable: boolean
): boolean {
    return !!savedViewKey && !isUnavailable && (!activeSavedViewId || activeSavedViewId !== hydratedSavedViewId);
}

export function isSavedViewUnavailable(activeSavedViewId: string | undefined, isMissing: boolean, isError: boolean): boolean {
    return isMissing || (isError && !activeSavedViewId);
}

export function savedViewColumnsEqual(
    a: ColumnVisibilityState | undefined,
    b: null | Record<string, boolean> | undefined,
    defaultColumnVisibility: ColumnVisibilityState = {}
): boolean {
    const normalize = (value: ColumnVisibilityState | null | undefined) =>
        Object.fromEntries(
            Object.entries({
                ...defaultColumnVisibility,
                ...(value ?? {})
            }).filter(([, isVisible]) => !isVisible)
        );
    const aEntries = Object.entries(normalize(a)).sort(([k1], [k2]) => k1.localeCompare(k2));
    const bEntries = Object.entries(normalize(b)).sort(([k1], [k2]) => k1.localeCompare(k2));

    if (aEntries.length !== bEntries.length) {
        return false;
    }

    return aEntries.every(([k, v], i) => {
        const bEntry = bEntries[i];
        return bEntry !== undefined && bEntry[0] === k && bEntry[1] === v;
    });
}

export function setSortQueryParam(queryParams: SavedViewQueryParams, value: null | string, history?: 'push' | 'replace'): void {
    if (supportsSortQueryParam(queryParams)) {
        if (queryParams.update) {
            queryParams.update(
                {
                    sort: value
                },
                {
                    history
                }
            );
        } else {
            queryParams.sort = value;
        }
    }
}

export function setTimeQueryParam(queryParams: SavedViewQueryParams, value: null | string): void {
    if (supportsTimeQueryParam(queryParams)) {
        queryParams.time = value;
    }
}

export function supportsSortQueryParam(queryParams: SavedViewQueryParams): queryParams is SavedViewQueryParams & { sort: null | string | undefined } {
    return Object.prototype.hasOwnProperty.call(queryParams, 'sort');
}

export function supportsTimeQueryParam(queryParams: SavedViewQueryParams): queryParams is SavedViewQueryParams & { time: null | string | undefined } {
    return Object.prototype.hasOwnProperty.call(queryParams, 'time');
}

export function useSavedViews(options: UseSavedViewsOptions): UseSavedViewsReturn {
    const isEnabled = $derived(!!organization.current);
    let autoFillColumnId = $state<AutoFillColumnSelection>(options.defaultAutoFillColumnId ?? null);
    let wrappedColumnIds = $state<WrappedColumnIds>([]);
    const currentUserQuery = getMeQuery();

    // Some routes, such as stream, do not declare every saved-view query parameter.
    const supportsSaved = supportsSavedQueryParam(options.queryParams);
    const supportsSort = supportsSortQueryParam(options.queryParams);
    const supportsTime = supportsTimeQueryParam(options.queryParams);

    const savedViewsListQuery = getSavedViewsByViewQuery({
        route: {
            get organizationId() {
                return organization.current;
            },
            get view() {
                return options.view;
            }
        }
    });

    const activeSavedView = $derived.by(() => {
        const views = savedViewsListQuery.data;
        if (!views) {
            return undefined;
        }

        if (options.slug) {
            return views.find((view) => savedViewResolvedSlug(view) === options.slug);
        }

        if (options.queryParams.saved) {
            return views.find((view) => view.id === options.queryParams.saved);
        }

        return undefined;
    });

    function applyColumnState(view: Pick<SavedView, 'columns'> | undefined): void {
        const availableColumnIds = options.getAvailableColumnIds?.() ?? options.getColumnOrder?.() ?? [];

        if (options.setColumnVisibility) {
            options.setColumnVisibility(filterAvailableColumnRecord(getSavedColumnVisibility(view), availableColumnIds));
        }

        if (options.setColumnOrder) {
            options.setColumnOrder(resolveAvailableColumnOrder(getSavedColumnOrder(view), availableColumnIds));
        }

        options.setColumnSizing?.(filterAvailableColumnRecord(getSavedColumnSizing(view), availableColumnIds));
        autoFillColumnId = resolveAvailableAutoFillColumnSelection(
            getSavedAutoFillColumnSelection(view, options.defaultAutoFillColumnId),
            availableColumnIds,
            options.defaultAutoFillColumnId
        );
        wrappedColumnIds = filterAvailableColumnIds(getSavedWrappedColumnIds(view), availableColumnIds);
    }

    function applyDisplayState(view: Pick<SavedView, 'show_chart' | 'show_stats'> | undefined): void {
        options.setShowStats?.(view?.show_stats ?? true);
        options.setShowChart?.(view?.show_chart ?? true);
    }

    function getDraftIdentity(view: SavedView): SavedViewDraftIdentity | undefined {
        const organizationId = organization.current;
        const userId = currentUserQuery.data?.id;
        if (!organizationId || !userId) {
            return undefined;
        }

        return {
            organizationId,
            savedViewId: view.id,
            userId
        };
    }

    function getServerFilters(view: SavedView): IFilter[] {
        if (view.filter_definitions) {
            return deserializeFilters(view.filter_definitions);
        }

        const filter = getComparableSavedViewFilter(view.filter, view.filter_definitions, options.defaultFilter);
        const filters = getFiltersFromCache(options.filterCacheKey(filter), filter);
        return applyTimeFilter(filters, getComparableSavedViewTime(view.time, options.defaultTime));
    }

    function getExplicitFilterOverrideKeys(serverFilters: IFilter[], currentFilters: IFilter[], draft: SavedViewDraft | undefined): string[] {
        const keys: string[] = [];
        const queryParameterKeys = [
            ['bot', 'boolean-bot'],
            ['first', 'boolean-first'],
            ['level', 'level'],
            ['project', 'project'],
            ['reference', 'reference'],
            ['session', 'session'],
            ['stack', 'string-stack'],
            ['status', 'status'],
            ['tag', 'tag'],
            ['time', 'date-date'],
            ['type', 'type'],
            ['version', 'version-version']
        ] as const;
        const addKey = (key: string) => {
            if (!keys.includes(key)) {
                keys.push(key);
            }
        };

        for (const [parameter, key] of queryParameterKeys) {
            if (page.url.searchParams.has(parameter)) {
                addKey(key);
            }
        }

        if (page.url.searchParams.has('filter')) {
            const filter = page.url.searchParams.get('filter') ?? '';
            if (filter) {
                for (const override of getFiltersFromCache(options.filterCacheKey(filter), filter)) {
                    addKey(override.key);
                }
            } else {
                for (const key of getEmptyFilterOverrideKeys(serverFilters, currentFilters, draft)) {
                    addKey(key);
                }
            }
        }

        if (page.url.searchParams.has('filters')) {
            const definitions = page.url.searchParams.get('filters');
            if (definitions) {
                try {
                    for (const override of deserializeFilters(definitions)) {
                        addKey(override.key);
                    }
                } catch {
                    // Ignore malformed URL state and let the route's normal parsing handle it.
                }
            }
        }

        return keys;
    }

    function applyDraftState(view: SavedView, draft: SavedViewDraft | undefined, overrideKeys: string[]): void {
        const availableColumnIds = options.getAvailableColumnIds?.() ?? options.getColumnOrder?.() ?? [];

        if (options.applyFilters) {
            const serverFilters = getServerFilters(view);
            const draftFilters = applyFilterChanges(serverFilters, draft?.filterChanges);
            const currentFilters = options.getFilterDefinitions ? deserializeFilters(options.getFilterDefinitions()) : [];
            options.applyFilters(mergeFilterOverrides(draftFilters, currentFilters, overrideKeys), {
                history: 'replace'
            });
        }

        if (options.setColumnVisibility) {
            options.setColumnVisibility(
                filterAvailableColumnRecord(applyRecordChanges(getSavedColumnVisibility(view), draft?.columnVisibilityChanges), availableColumnIds)
            );
        }

        if (options.setColumnOrder) {
            options.setColumnOrder(resolveAvailableColumnOrder(draft?.columnOrder ?? getSavedColumnOrder(view), availableColumnIds));
        }

        options.setColumnSizing?.(filterAvailableColumnRecord(applyRecordChanges(getSavedColumnSizing(view), draft?.columnSizingChanges), availableColumnIds));
        autoFillColumnId = resolveAvailableAutoFillColumnSelection(
            draft && 'autoFillColumnId' in draft ? draft.autoFillColumnId! : getSavedAutoFillColumnSelection(view, options.defaultAutoFillColumnId),
            availableColumnIds,
            options.defaultAutoFillColumnId
        );
        wrappedColumnIds = filterAvailableColumnIds(applyWrappedColumnChanges(getSavedWrappedColumnIds(view), draft?.wrappedColumnChanges), availableColumnIds);

        options.setShowStats?.(draft && 'showStats' in draft ? draft.showStats! : (view.show_stats ?? true));
        options.setShowChart?.(draft && 'showChart' in draft ? draft.showChart! : (view.show_chart ?? true));

        if (draft && 'sort' in draft && !page.url.searchParams.has('sort')) {
            setSortQueryParam(options.queryParams, getDraftSortQueryParam(view.sort ?? null, draft.sort ?? null), 'replace');
        }
    }

    function getActiveFilterOverrideKeys(currentFilters: IFilter[]): string[] {
        return getMatchingFilterOverrideKeys(currentFilters, activeFilterOverrideBaselines);
    }

    function getStoredDraft(view: SavedView): SavedViewDraft | undefined {
        const identity = getDraftIdentity(view);
        return identity ? getSavedViewDraft(identity) : undefined;
    }

    function buildSavedViewDraft(
        view: SavedView,
        columnOrderBaseline: ColumnOrderState | undefined = hydratedColumnOrder,
        preserveExplicitFilterOverrides = false
    ): SavedViewDraft | undefined {
        const draft: SavedViewDraft = {
            version: 1
        };
        const storedDraft = preserveExplicitFilterOverrides ? getStoredDraft(view) : undefined;
        const availableColumnIds = options.getAvailableColumnIds?.() ?? options.getColumnOrder?.() ?? [];

        if (options.getFilterDefinitions) {
            const serverFilters = getServerFilters(view);
            let currentFilters = deserializeFilters(options.getFilterDefinitions());
            if (preserveExplicitFilterOverrides) {
                const storedDraftFilters = applyFilterChanges(serverFilters, storedDraft?.filterChanges);
                currentFilters = mergeFilterOverrides(currentFilters, storedDraftFilters, getActiveFilterOverrideKeys(currentFilters));
            }

            draft.filterChanges = buildFilterChanges(serverFilters, currentFilters);
        }

        if (options.getColumnVisibility) {
            const serverVisibility = filterAvailableColumnRecord(
                {
                    ...options.defaultColumnVisibility,
                    ...getSavedColumnVisibility(view)
                },
                availableColumnIds
            );
            const currentVisibility = filterAvailableColumnRecord(
                {
                    ...options.defaultColumnVisibility,
                    ...options.getColumnVisibility()
                },
                availableColumnIds
            );
            draft.columnVisibilityChanges = buildRecordChanges(serverVisibility, currentVisibility);
        }

        if (options.getColumnOrder && columnOrderBaseline && !columnOrdersEqual(options.getColumnOrder(), columnOrderBaseline)) {
            draft.columnOrder = [...options.getColumnOrder()];
        }

        if (options.getColumnSizing) {
            draft.columnSizingChanges = buildRecordChanges(
                filterAvailableColumnRecord(getSavedColumnSizing(view), availableColumnIds),
                filterAvailableColumnRecord(normalizeColumnSizing(options.getColumnSizing()), availableColumnIds)
            );
        }

        if (hasSavedViewAutoFillChange(autoFillColumnId, view, options.defaultAutoFillColumnId, availableColumnIds)) {
            draft.autoFillColumnId = autoFillColumnId;
        }

        draft.wrappedColumnChanges = buildWrappedColumnChanges(
            filterAvailableColumnIds(getSavedWrappedColumnIds(view), availableColumnIds),
            filterAvailableColumnIds(wrappedColumnIds, availableColumnIds)
        );

        if (options.getShowStats && options.getShowStats() !== (view.show_stats ?? true)) {
            draft.showStats = options.getShowStats();
        }

        if (options.getShowChart && options.getShowChart() !== (view.show_chart ?? true)) {
            draft.showChart = options.getShowChart();
        }

        if (supportsSort) {
            const currentSort = options.getSort?.() ?? options.queryParams.sort ?? null;
            const serverSort = view.sort ?? null;
            const sortForDraft = preserveExplicitFilterOverrides ? getDraftSortValue(serverSort, currentSort, activeSortOverride, storedDraft) : currentSort;
            if (sortForDraft !== serverSort) {
                draft.sort = sortForDraft;
            }
        }

        return Object.entries(draft).some(([key, value]) => key !== 'version' && value !== undefined) ? draft : undefined;
    }

    async function captureHydratedColumnOrderAfterStateSettles(viewId: string): Promise<void> {
        await tick();

        if (activeSavedView?.id !== viewId || serverHydratedSavedViewId !== viewId) {
            return;
        }

        hydratedColumnOrder = options.getColumnOrder ? [...options.getColumnOrder()] : undefined;
    }

    // Hydrate saved view state when a saved view loads. Query params remain URL overrides.
    // lastLoadedViewId prevents re-hydration on background refetches (which would stomp user edits).
    let lastLoadedViewId = '';
    let activeFilterOverrideBaselines = $state<Record<string, string>>({});
    let activeSortOverride = $state<{ value: null | string }>();
    let appliedDraftKey = $state('');
    let pendingDraftKey = '';
    let pendingDraftGeneration = -1;
    let draftHydrationGeneration = $state(0);
    let draftPersistenceGeneration = 0;
    let hydratedColumnOrder = $state<ColumnOrderState>();
    let hydratedSavedView = $state<SavedView>();
    let hydratedSavedViewSignature = '';
    let hydratedSavedViewId = $state<string>();
    let serverHydratedSavedViewId = $state<string>();
    $effect(() => {
        const savedViewKey = options.slug ?? options.queryParams.saved;
        const view = activeSavedView;
        const isLoading = savedViewsListQuery.isLoading;
        const isFetching = savedViewsListQuery.isFetching;
        const views = savedViewsListQuery.data;

        if (!savedViewKey || isLoading || !views) {
            if (!savedViewKey) {
                if (lastLoadedViewId !== '') {
                    applyColumnState(undefined);
                    applyDisplayState(undefined);
                    pendingDraftKey = '';
                    pendingDraftGeneration = -1;
                    draftHydrationGeneration++;
                }

                lastLoadedViewId = '';
                activeFilterOverrideBaselines = {};
                activeSortOverride = undefined;
                appliedDraftKey = '';
                hydratedColumnOrder = undefined;
                hydratedSavedView = undefined;
                hydratedSavedViewSignature = '';
            }

            hydratedSavedViewId = undefined;
            serverHydratedSavedViewId = undefined;
            return;
        }

        if (!view) {
            // Skip while refetching to avoid false-positive clears during cache invalidation
            if (isFetching) {
                return;
            }

            hydratedSavedViewId = undefined;
            serverHydratedSavedViewId = undefined;
            return;
        }

        const viewSignature = getSavedViewStateSignature(view);

        // Keep the last hydrated server content as the local-edit baseline. If a
        // same-ID update matches the current UI, it was saved and becomes the new
        // baseline. Otherwise leave the UI and baseline alone until navigation so
        // remote changes cannot be persisted back as local reverse edits.
        if (view.id === lastLoadedViewId) {
            const resolvedServerColumnOrder = options.getColumnOrder ? resolveSavedViewColumnOrder(view, options.getColumnOrder()) : undefined;
            if (viewSignature !== hydratedSavedViewSignature && untrack(() => buildSavedViewDraft(view, resolvedServerColumnOrder) === undefined)) {
                hydratedColumnOrder = resolvedServerColumnOrder;
                hydratedSavedView = view;
                hydratedSavedViewSignature = viewSignature;
            }

            serverHydratedSavedViewId = view.id;
            return;
        }

        lastLoadedViewId = view.id;
        activeFilterOverrideBaselines = {};
        activeSortOverride = undefined;
        appliedDraftKey = '';
        pendingDraftKey = '';
        pendingDraftGeneration = -1;
        draftHydrationGeneration++;
        hydratedColumnOrder = undefined;
        hydratedSavedView = view;
        hydratedSavedViewSignature = viewSignature;

        if (view.filter_definitions) {
            try {
                const hydrated = deserializeFilters(view.filter_definitions);
                updateSavedViewFilterCache(options, view, hydrated);
            } catch {
                console.error('Failed to deserialize saved view filter definitions');
            }
        }

        applyColumnState(view);
        applyDisplayState(view);
        hydratedSavedViewId = undefined;
        serverHydratedSavedViewId = view.id;
        void captureHydratedColumnOrderAfterStateSettles(view.id);
    });

    async function applyDraftAfterViewHydrates(viewId: string, identity: SavedViewDraftIdentity, draftKey: string, generation: number): Promise<void> {
        // A dirty source view can leave a queued query-param reset behind during
        // client-side navigation. Let that navigation task and route hydration
        // finish before applying the destination view's browser-local draft.
        await tick();
        await new Promise<void>((resolve) => setTimeout(resolve, 0));
        await tick();

        const view = activeSavedView;
        const currentIdentity = view ? getDraftIdentity(view) : undefined;
        const currentDraftKey = currentIdentity ? `${currentIdentity.userId}:${currentIdentity.organizationId}:${currentIdentity.savedViewId}` : undefined;
        if (!view || view.id !== viewId || serverHydratedSavedViewId !== view.id || currentDraftKey !== draftKey || draftHydrationGeneration !== generation) {
            if (pendingDraftKey === draftKey && pendingDraftGeneration === generation) {
                pendingDraftKey = '';
                pendingDraftGeneration = -1;
            }
            return;
        }

        const pendingEdits = hydratedSavedView?.id === view.id ? buildSavedViewDraft(hydratedSavedView, hydratedColumnOrder) : undefined;
        const draft = mergePendingSavedViewDraftEdits(getSavedViewDraft(identity), pendingEdits);
        const serverFilters = getServerFilters(view);
        const currentFilters = options.getFilterDefinitions ? deserializeFilters(options.getFilterDefinitions()) : [];
        const overrideKeys = getExplicitFilterOverrideKeys(serverFilters, currentFilters, draft);
        activeFilterOverrideBaselines = buildFilterOverrideBaselines(currentFilters, overrideKeys);
        activeSortOverride = page.url.searchParams.has('sort')
            ? {
                  value: options.getSort?.() ?? options.queryParams.sort ?? null
              }
            : undefined;
        hydratedColumnOrder = options.getColumnOrder ? resolveSavedViewColumnOrder(view, options.getColumnOrder()) : undefined;
        hydratedSavedView = view;
        hydratedSavedViewSignature = getSavedViewStateSignature(view);
        appliedDraftKey = draftKey;
        if (pendingDraftKey === draftKey && pendingDraftGeneration === generation) {
            pendingDraftKey = '';
            pendingDraftGeneration = -1;
        }

        applyDraftState(view, draft, overrideKeys);

        hydratedSavedViewId = view.id;
    }

    $effect(() => {
        const generation = draftHydrationGeneration;
        const view = activeSavedView;
        if (!view || serverHydratedSavedViewId !== view.id) {
            return;
        }

        const identity = getDraftIdentity(view);
        if (!identity) {
            if (currentUserQuery.isError) {
                hydratedSavedViewId = view.id;
            }

            return;
        }

        const draftKey = `${identity.userId}:${identity.organizationId}:${identity.savedViewId}`;
        if (appliedDraftKey === draftKey || (pendingDraftKey === draftKey && pendingDraftGeneration === generation)) {
            return;
        }

        pendingDraftKey = draftKey;
        pendingDraftGeneration = generation;
        void applyDraftAfterViewHydrates(view.id, identity, draftKey, generation);
    });

    // Detect if current filters or columns differ from the active saved view
    const isModified = $derived.by(() => {
        const view = hydratedSavedView;
        if (!view || activeSavedView?.id !== view.id) {
            return false;
        }
        const availableColumnIds = options.getAvailableColumnIds?.() ?? options.getColumnOrder?.() ?? [];

        const savedViewFilter = getComparableSavedViewFilter(view.filter, view.filter_definitions, options.defaultFilter);
        if ((options.getFilter?.() ?? options.queryParams.filter ?? null) !== savedViewFilter) {
            return true;
        }

        const savedViewTime = getComparableSavedViewTime(view.time, options.defaultTime);
        if (supportsTime && (options.getTime?.() ?? options.queryParams.time ?? null) !== savedViewTime) {
            return true;
        }

        if (supportsSort && (options.getSort?.() ?? options.queryParams.sort ?? null) !== (view.sort ?? null)) {
            return true;
        }

        if (options.getFilterDefinitions && view.filter_definitions && !filterDefinitionsEqual(options.getFilterDefinitions(), view.filter_definitions)) {
            return true;
        }

        if (
            options.getColumnVisibility &&
            hasSavedViewColumnChanges(
                filterAvailableColumnRecord(options.getColumnVisibility(), availableColumnIds),
                filterAvailableColumnRecord(getSavedColumnVisibility(view), availableColumnIds),
                filterAvailableColumnRecord(options.defaultColumnVisibility ?? {}, availableColumnIds)
            )
        ) {
            return true;
        }

        if (options.getColumnOrder && hydratedColumnOrder && !columnOrdersEqual(options.getColumnOrder(), hydratedColumnOrder)) {
            return true;
        }

        if (options.getColumnSizing && !savedViewColumnSizingEqual(options.getColumnSizing(), view, availableColumnIds)) {
            return true;
        }

        if (hasSavedViewAutoFillChange(autoFillColumnId, view, options.defaultAutoFillColumnId, availableColumnIds)) {
            return true;
        }

        if (!savedViewColumnWrappingEqual(wrappedColumnIds, view, availableColumnIds)) {
            return true;
        }

        if (options.getShowStats && options.getShowStats() !== (view.show_stats ?? true)) {
            return true;
        }

        if (options.getShowChart && options.getShowChart() !== (view.show_chart ?? true)) {
            return true;
        }

        return false;
    });

    const currentDraft = $derived.by(() => {
        const view = hydratedSavedView;
        return view && activeSavedView?.id === view.id ? buildSavedViewDraft(view, hydratedColumnOrder, true) : undefined;
    });

    $effect(() => {
        if (!appliedDraftKey) {
            return;
        }

        if (options.getFilterDefinitions) {
            const currentFilters = deserializeFilters(options.getFilterDefinitions());
            const activeKeys = getActiveFilterOverrideKeys(currentFilters);
            const remainingBaselines = Object.fromEntries(Object.entries(activeFilterOverrideBaselines).filter(([key]) => activeKeys.includes(key)));
            if (Object.keys(remainingBaselines).length !== Object.keys(activeFilterOverrideBaselines).length) {
                activeFilterOverrideBaselines = remainingBaselines;
            }
        }

        const currentSort = options.getSort?.() ?? options.queryParams.sort ?? null;
        if (activeSortOverride && currentSort !== activeSortOverride.value) {
            activeSortOverride = undefined;
        }
    });

    async function persistDraftAfterStateSettles(
        viewId: string,
        identity: SavedViewDraftIdentity,
        draftKey: string,
        draft: SavedViewDraft | undefined,
        generation: number
    ): Promise<void> {
        await tick();

        const view = activeSavedView;
        if (!view || view.id !== viewId || hydratedSavedViewId !== view.id || appliedDraftKey !== draftKey || draftPersistenceGeneration !== generation) {
            return;
        }

        if (draft) {
            saveSavedViewDraft(identity, draft);
        } else {
            clearSavedViewDraft(identity);
        }
    }

    $effect(() => {
        const view = activeSavedView;
        if (!view || hydratedSavedViewId !== view.id) {
            return;
        }

        const identity = getDraftIdentity(view);
        const draftKey = identity ? `${identity.userId}:${identity.organizationId}:${identity.savedViewId}` : undefined;
        if (!identity || !draftKey || appliedDraftKey !== draftKey) {
            return;
        }

        // The draft value tracks every persisted field even while isModified remains true.
        void persistDraftAfterStateSettles(view.id, identity, draftKey, currentDraft, draftPersistenceGeneration);
    });

    const isMissing = $derived(
        hasMissingSavedView({
            activeSavedView,
            isLoading: savedViewsListQuery.isLoading,
            savedViewKey: options.slug ?? options.queryParams.saved,
            savedViews: savedViewsListQuery.data
        })
    );

    function handleLoadView(view: SavedView) {
        if (options.baseHref) {
            goto(savedViewHref(view));
            return;
        }

        options.queryParams.saved = view.id;
    }

    function rehydrateQueryOverrides() {
        const view = activeSavedView;
        if (!view || serverHydratedSavedViewId !== view.id) {
            return;
        }

        hydratedSavedViewId = undefined;
        appliedDraftKey = '';
        pendingDraftKey = '';
        pendingDraftGeneration = -1;
        draftHydrationGeneration++;
    }

    afterNavigate(({ from, to }) => {
        if (!from?.url || !to?.url || from.url.pathname !== to.url.pathname) {
            return;
        }

        if (supportsSaved && from.url.searchParams.get('saved') !== to.url.searchParams.get('saved')) {
            return;
        }

        if (getSavedViewOverrideSignature(from.url) !== getSavedViewOverrideSignature(to.url)) {
            rehydrateQueryOverrides();
        }
    });

    function handleResetToSaved() {
        const view = activeSavedView;
        if (!view) {
            return;
        }

        const identity = getDraftIdentity(view);
        if (!identity) {
            return;
        }

        draftPersistenceGeneration++;
        hydratedSavedView = view;
        hydratedSavedViewSignature = getSavedViewStateSignature(view);
        clearSavedViewDraft(identity);

        if (view.filter_definitions) {
            try {
                const hydrated = deserializeFilters(view.filter_definitions);
                updateSavedViewFilterCache(options, view, hydrated);
            } catch {
                console.error('Failed to deserialize saved view filter definitions');
            }
        }

        options.queryParams.filter = null;
        options.queryParams.filters = null;
        setSortQueryParam(options.queryParams, null);
        setTimeQueryParam(options.queryParams, null);
        activeFilterOverrideBaselines = {};
        activeSortOverride = undefined;
        hydratedColumnOrder = undefined;
        applyColumnState(view);
        applyDisplayState(view);
        void captureHydratedColumnOrderAfterStateSettles(view.id);
    }

    function handleSavedViewUpdated(view: SavedView) {
        draftPersistenceGeneration++;
        const organizationId = organization.current;
        const identity =
            getDraftIdentity(view) ??
            (organizationId && view.updated_by_user_id
                ? {
                      organizationId,
                      savedViewId: view.id,
                      userId: view.updated_by_user_id
                  }
                : undefined);
        if (identity) {
            clearSavedViewDraft(identity);
            appliedDraftKey = `${identity.userId}:${identity.organizationId}:${identity.savedViewId}`;
        }

        activeFilterOverrideBaselines = {};
        activeSortOverride = undefined;
        hydratedColumnOrder = options.getColumnOrder ? [...options.getColumnOrder()] : undefined;
        hydratedSavedView = view;
        hydratedSavedViewSignature = getSavedViewStateSignature(view);
        hydratedSavedViewId = view.id;
        serverHydratedSavedViewId = view.id;
    }

    function handleClearSavedView() {
        clearSavedViewQueryParams(options.queryParams);
        applyColumnState(undefined);
        applyDisplayState(undefined);

        if (options.baseHref) {
            goto(options.baseHref);
        }
    }

    return {
        get activeSavedView() {
            return activeSavedView;
        },
        get autoFillColumnId() {
            return autoFillColumnId;
        },
        get canModifySavedView() {
            return !!currentUserQuery.data?.id;
        },
        handleClearSavedView,
        handleLoadView,
        handleResetToSaved,
        handleSavedViewUpdated,
        get hydratedSavedViewId() {
            return hydratedSavedViewId;
        },
        get isEnabled() {
            return isEnabled;
        },
        get isError() {
            return savedViewsListQuery.isError;
        },
        get isLoading() {
            return savedViewsListQuery.isLoading;
        },
        get isMissing() {
            return isMissing;
        },
        get isModified() {
            return isModified;
        },
        get savedViews() {
            return savedViewsListQuery.data ?? [];
        },
        setAutoFillColumnId(columnId: AutoFillColumnSelection) {
            autoFillColumnId = columnId;
        },
        setWrappedColumnIds(columnIds: WrappedColumnIds) {
            wrappedColumnIds = columnIds.filter((columnId, index) => columnIds.indexOf(columnId) === index);
        },
        get wrappedColumnIds() {
            return wrappedColumnIds;
        }
    };
}

function normalizeFilterDefinitions(value: null | string | undefined): string {
    if (!value) {
        return '[]';
    }

    try {
        const parsed = JSON.parse(value);
        if (!Array.isArray(parsed) || parsed.length === 0) {
            return '[]';
        }

        return serializeFilters(sortFilterDefinitions(deserializeFilters(value)));
    } catch {
        return value;
    }
}

function sortFilterDefinitions(filters: IFilter[]): IFilter[] {
    return [...filters].sort((a, b) => a.key.localeCompare(b.key));
}

function supportsFiltersQueryParam(queryParams: SavedViewQueryParams): queryParams is SavedViewQueryParams & { filters: null | string | undefined } {
    return Object.prototype.hasOwnProperty.call(queryParams, 'filters');
}

function supportsSavedQueryParam(queryParams: SavedViewQueryParams): queryParams is SavedViewQueryParams & { saved: null | string | undefined } {
    return Object.prototype.hasOwnProperty.call(queryParams, 'saved');
}

function updateSavedViewFilterCache(options: UseSavedViewsOptions, view: SavedView, filters: IFilter[]): void {
    const currentRouteKey = options.filterCacheKey(view.filter ?? null);
    options.updateFilterCache(currentRouteKey, filters);

    const canonicalHref = savedViewHref(view);
    const queryIndex = canonicalHref.indexOf('?');
    const canonicalPath = queryIndex >= 0 ? canonicalHref.slice(0, queryIndex) : canonicalHref;
    const canonicalRouteKey = buildFilterCacheKey(organization.current, canonicalPath, view.filter ?? null);
    if (canonicalRouteKey !== currentRouteKey) {
        options.updateFilterCache(canonicalRouteKey, filters);
    }
}
