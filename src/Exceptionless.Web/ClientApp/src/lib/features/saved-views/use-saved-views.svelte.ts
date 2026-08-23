import type { IFilter } from '$comp/faceted-filter';
import type { ColumnOrderState, ColumnSizingState, ColumnVisibilityState } from '@tanstack/svelte-table';

import { goto } from '$app/navigation';
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
    getSavedAutoFillColumnSelection,
    getSavedColumnOrder,
    getSavedColumnSizing,
    getSavedColumnVisibility,
    getSavedWrappedColumnIds,
    normalizeColumnSizing,
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
}

export interface UseSavedViewsOptions {
    applyFilters?: (filters: IFilter[]) => void;
    baseHref?: string;
    defaultAutoFillColumnId?: string;
    defaultColumnVisibility?: ColumnVisibilityState;
    defaultFilter?: null | string;
    defaultTime?: null | string;
    filterCacheKey: (filter: null | string) => string;
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
    handleClearSavedView: () => void;
    handleLoadView: (view: SavedView) => void;
    handleResetToSaved: () => void;
    hydratedSavedViewId: string | undefined;
    isEnabled: boolean;
    isLoading: boolean;
    isMissing: boolean;
    isModified: boolean;
    savedViews: SavedView[];
    setAutoFillColumnId: (columnId: AutoFillColumnSelection) => void;
    setWrappedColumnIds: (columnIds: WrappedColumnIds) => void;
    wrappedColumnIds: WrappedColumnIds;
}

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

export function getDraftSortValue(
    serverSort: null | string,
    currentSort: null | string,
    initialOverride: undefined | { value: null | string },
    storedDraft: SavedViewDraft | undefined
): null | string {
    if (currentSort === serverSort || initialOverride?.value !== currentSort) {
        return currentSort;
    }

    return storedDraft && 'sort' in storedDraft ? (storedDraft.sort ?? null) : serverSort;
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

export function hasMissingSavedViewSlug(options: {
    activeSavedView: SavedView | undefined;
    isLoading: boolean;
    savedViews: SavedView[] | undefined;
    slug: string | undefined;
}): boolean {
    return !!options.slug && !options.activeSavedView && !!options.savedViews && !options.isLoading;
}

export function hasSavedViewAutoFillChange(current: AutoFillColumnSelection, view: Pick<SavedView, 'columns'>, defaultAutoFillColumnId?: string): boolean {
    return current !== getSavedAutoFillColumnSelection(view, defaultAutoFillColumnId);
}

export function hasSavedViewColumnChanges(
    current: ColumnVisibilityState | undefined,
    saved: null | Record<string, boolean> | undefined,
    defaultColumnVisibility: ColumnVisibilityState = {}
): boolean {
    return !savedViewColumnsEqual(current, saved, defaultColumnVisibility);
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

export function setSortQueryParam(queryParams: SavedViewQueryParams, value: null | string): void {
    if (supportsSortQueryParam(queryParams)) {
        queryParams.sort = value;
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
        if (options.setColumnVisibility) {
            options.setColumnVisibility(getSavedColumnVisibility(view));
        }

        if (options.setColumnOrder) {
            options.setColumnOrder(getSavedColumnOrder(view));
        }

        options.setColumnSizing?.(getSavedColumnSizing(view));
        autoFillColumnId = getSavedAutoFillColumnSelection(view, options.defaultAutoFillColumnId);
        wrappedColumnIds = getSavedWrappedColumnIds(view);
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

    function getExplicitFilterOverrideKeys(serverFilters: IFilter[]): string[] {
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
        const dedicatedQueryFilterKeys: string[] = queryParameterKeys.map(([, key]) => key);
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
                for (const serverFilter of serverFilters) {
                    if (serverFilter.type !== 'date' && !dedicatedQueryFilterKeys.includes(serverFilter.key)) {
                        addKey(serverFilter.key);
                    }
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

    function applyDraftState(view: SavedView, draft: SavedViewDraft, overrideKeys: string[]): void {
        if (draft.filterChanges && options.applyFilters) {
            const serverFilters = getServerFilters(view);
            const draftFilters = applyFilterChanges(serverFilters, draft.filterChanges);
            const currentFilters = options.getFilterDefinitions ? deserializeFilters(options.getFilterDefinitions()) : [];
            options.applyFilters(mergeFilterOverrides(draftFilters, currentFilters, overrideKeys));
        }

        if (options.setColumnVisibility) {
            options.setColumnVisibility(applyRecordChanges(getSavedColumnVisibility(view), draft.columnVisibilityChanges));
        }

        if (options.setColumnOrder) {
            options.setColumnOrder(draft.columnOrder ?? getSavedColumnOrder(view));
        }

        options.setColumnSizing?.(applyRecordChanges(getSavedColumnSizing(view), draft.columnSizingChanges));
        autoFillColumnId = 'autoFillColumnId' in draft ? draft.autoFillColumnId! : getSavedAutoFillColumnSelection(view, options.defaultAutoFillColumnId);
        wrappedColumnIds = applyWrappedColumnChanges(getSavedWrappedColumnIds(view), draft.wrappedColumnChanges);

        if ('showStats' in draft) {
            options.setShowStats?.(draft.showStats!);
        }

        if ('showChart' in draft) {
            options.setShowChart?.(draft.showChart!);
        }

        if ('sort' in draft && !page.url.searchParams.has('sort')) {
            setSortQueryParam(options.queryParams, draft.sort === (view.sort ?? null) ? null : (draft.sort ?? null));
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

        if (options.getFilterDefinitions) {
            const serverFilters = getServerFilters(view);
            let currentFilters = deserializeFilters(options.getFilterDefinitions());
            const currentFilterChanges = buildFilterChanges(serverFilters, currentFilters);
            if (preserveExplicitFilterOverrides && currentFilterChanges) {
                const storedDraftFilters = applyFilterChanges(serverFilters, storedDraft?.filterChanges);
                currentFilters = mergeFilterOverrides(currentFilters, storedDraftFilters, getActiveFilterOverrideKeys(currentFilters));
            }

            draft.filterChanges = buildFilterChanges(serverFilters, currentFilters);
        }

        if (options.getColumnVisibility) {
            const serverVisibility = {
                ...options.defaultColumnVisibility,
                ...getSavedColumnVisibility(view)
            };
            const currentVisibility = {
                ...options.defaultColumnVisibility,
                ...options.getColumnVisibility()
            };
            draft.columnVisibilityChanges = buildRecordChanges(serverVisibility, currentVisibility);
        }

        if (options.getColumnOrder && columnOrderBaseline && !columnOrdersEqual(options.getColumnOrder(), columnOrderBaseline)) {
            draft.columnOrder = [...options.getColumnOrder()];
        }

        if (options.getColumnSizing) {
            draft.columnSizingChanges = buildRecordChanges(getSavedColumnSizing(view), normalizeColumnSizing(options.getColumnSizing()));
        }

        if (hasSavedViewAutoFillChange(autoFillColumnId, view, options.defaultAutoFillColumnId)) {
            draft.autoFillColumnId = autoFillColumnId;
        }

        draft.wrappedColumnChanges = buildWrappedColumnChanges(getSavedWrappedColumnIds(view), wrappedColumnIds);

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

    async function applyDraftAfterViewHydrates(viewId: string, identity: SavedViewDraftIdentity, draftKey: string): Promise<void> {
        // A dirty source view can leave a queued query-param reset behind during
        // client-side navigation. Let that navigation task and route hydration
        // finish before applying the destination view's browser-local draft.
        await tick();
        await new Promise<void>((resolve) => setTimeout(resolve, 0));
        await tick();

        const view = activeSavedView;
        const currentIdentity = view ? getDraftIdentity(view) : undefined;
        const currentDraftKey = currentIdentity ? `${currentIdentity.userId}:${currentIdentity.organizationId}:${currentIdentity.savedViewId}` : undefined;
        if (!view || view.id !== viewId || serverHydratedSavedViewId !== view.id || currentDraftKey !== draftKey) {
            if (pendingDraftKey === draftKey) {
                pendingDraftKey = '';
            }
            return;
        }

        const draft = getSavedViewDraft(identity);
        const serverFilters = getServerFilters(view);
        const currentFilters = options.getFilterDefinitions ? deserializeFilters(options.getFilterDefinitions()) : [];
        const overrideKeys = getExplicitFilterOverrideKeys(serverFilters);
        activeFilterOverrideBaselines = buildFilterOverrideBaselines(currentFilters, overrideKeys);
        activeSortOverride = page.url.searchParams.has('sort')
            ? {
                  value: options.getSort?.() ?? options.queryParams.sort ?? null
              }
            : undefined;
        appliedDraftKey = draftKey;
        if (pendingDraftKey === draftKey) {
            pendingDraftKey = '';
        }

        if (draft) {
            applyDraftState(view, draft, overrideKeys);
        }

        hydratedSavedViewId = view.id;
    }

    $effect(() => {
        const view = activeSavedView;
        if (!view || serverHydratedSavedViewId !== view.id) {
            return;
        }

        const identity = getDraftIdentity(view);
        if (!identity) {
            return;
        }

        const draftKey = `${identity.userId}:${identity.organizationId}:${identity.savedViewId}`;
        if (appliedDraftKey === draftKey || pendingDraftKey === draftKey) {
            return;
        }

        pendingDraftKey = draftKey;
        void applyDraftAfterViewHydrates(view.id, identity, draftKey);
    });

    // Detect if current filters or columns differ from the active saved view
    const isModified = $derived.by(() => {
        const view = hydratedSavedView;
        if (!view || activeSavedView?.id !== view.id) {
            return false;
        }

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
            hasSavedViewColumnChanges(options.getColumnVisibility(), getSavedColumnVisibility(view), options.defaultColumnVisibility)
        ) {
            return true;
        }

        if (options.getColumnOrder && hydratedColumnOrder && !columnOrdersEqual(options.getColumnOrder(), hydratedColumnOrder)) {
            return true;
        }

        if (options.getColumnSizing && !savedViewColumnSizingEqual(options.getColumnSizing(), view)) {
            return true;
        }

        if (hasSavedViewAutoFillChange(autoFillColumnId, view, options.defaultAutoFillColumnId)) {
            return true;
        }

        if (!savedViewColumnWrappingEqual(wrappedColumnIds, view)) {
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
        draft: SavedViewDraft | undefined
    ): Promise<void> {
        await tick();

        const view = activeSavedView;
        if (!view || view.id !== viewId || hydratedSavedViewId !== view.id || appliedDraftKey !== draftKey) {
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
        void persistDraftAfterStateSettles(view.id, identity, draftKey, currentDraft);
    });

    const isMissing = $derived(
        hasMissingSavedViewSlug({
            activeSavedView,
            isLoading: savedViewsListQuery.isLoading,
            savedViews: savedViewsListQuery.data,
            slug: options.slug
        })
    );

    function handleLoadView(view: SavedView) {
        if (options.baseHref) {
            goto(savedViewHref(view));
            return;
        }

        options.queryParams.saved = view.id;
    }

    function handleResetToSaved() {
        const view = activeSavedView;
        if (!view) {
            return;
        }

        hydratedSavedView = view;
        hydratedSavedViewSignature = getSavedViewStateSignature(view);
        const identity = getDraftIdentity(view);
        if (identity) {
            clearSavedViewDraft(identity);
        }

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
        handleClearSavedView,
        handleLoadView,
        handleResetToSaved,
        get hydratedSavedViewId() {
            return hydratedSavedViewId;
        },
        get isEnabled() {
            return isEnabled;
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
