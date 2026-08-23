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
import { tick } from 'svelte';

import type { AutoFillColumnSelection, WrappedColumnIds } from './column-settings';
import type { SavedView } from './models';

import { getSavedViewsByViewQuery } from './api.svelte';
import {
    getSavedAutoFillColumnSelection,
    getSavedColumnOrder,
    getSavedColumnSizing,
    getSavedColumnVisibility,
    getSavedWrappedColumnIds,
    savedViewColumnOrderEqual,
    savedViewColumnSizingEqual,
    savedViewColumnWrappingEqual
} from './column-settings';
import {
    applyFilterChanges,
    applyRecordChanges,
    applyWrappedColumnChanges,
    buildFilterChanges,
    buildRecordChanges,
    buildWrappedColumnChanges,
    clearSavedViewDraft,
    getSavedViewDraft,
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

    function hasExplicitFilterOverrides(): boolean {
        const filterParameterNames = [
            'bot',
            'filter',
            'filters',
            'first',
            'level',
            'project',
            'reference',
            'session',
            'stack',
            'status',
            'tag',
            'time',
            'type',
            'version'
        ];

        return filterParameterNames.some((name) => page.url.searchParams.has(name));
    }

    function applyDraftState(view: SavedView, draft: SavedViewDraft): void {
        if (draft.filterChanges && options.applyFilters && !hasExplicitFilterOverrides()) {
            options.applyFilters(applyFilterChanges(getServerFilters(view), draft.filterChanges));
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

    function buildSavedViewDraft(view: SavedView): SavedViewDraft | undefined {
        const draft: SavedViewDraft = {
            version: 1
        };

        if (options.getFilterDefinitions) {
            const currentFilters = deserializeFilters(options.getFilterDefinitions());
            draft.filterChanges = buildFilterChanges(getServerFilters(view), currentFilters);
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

        if (options.getColumnOrder && !savedViewColumnOrderEqual(options.getColumnOrder(), view)) {
            draft.columnOrder = [...options.getColumnOrder()];
        }

        if (options.getColumnSizing) {
            draft.columnSizingChanges = buildRecordChanges(getSavedColumnSizing(view), options.getColumnSizing());
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
            if (currentSort !== (view.sort ?? null)) {
                draft.sort = currentSort;
            }
        }

        return Object.entries(draft).some(([key, value]) => key !== 'version' && value !== undefined) ? draft : undefined;
    }

    // Hydrate saved view state when a saved view loads. Query params remain URL overrides.
    // lastLoadedViewId prevents re-hydration on background refetches (which would stomp user edits).
    let lastLoadedViewId = '';
    let appliedDraftKey = $state('');
    let pendingDraftKey = '';
    let hydratedSavedViewId = $state<string>();
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
                appliedDraftKey = '';
            }

            hydratedSavedViewId = undefined;
            return;
        }

        if (!view) {
            hydratedSavedViewId = undefined;
            // Skip while refetching to avoid false-positive clears during cache invalidation
            if (isFetching) {
                return;
            }

            return;
        }

        // Already loaded this view — skip to avoid stomping user edits on background refetch
        if (view.id === lastLoadedViewId) {
            hydratedSavedViewId = view.id;
            return;
        }

        lastLoadedViewId = view.id;

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
        hydratedSavedViewId = view.id;
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
        if (!view || view.id !== viewId || hydratedSavedViewId !== view.id || currentDraftKey !== draftKey) {
            if (pendingDraftKey === draftKey) {
                pendingDraftKey = '';
            }
            return;
        }

        const draft = getSavedViewDraft(identity);
        appliedDraftKey = draftKey;
        if (pendingDraftKey === draftKey) {
            pendingDraftKey = '';
        }

        if (draft) {
            applyDraftState(view, draft);
        }
    }

    $effect(() => {
        const view = activeSavedView;
        if (!view || hydratedSavedViewId !== view.id) {
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
        const view = activeSavedView;
        if (!view) {
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

        if (options.getColumnOrder && !savedViewColumnOrderEqual(options.getColumnOrder(), view)) {
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

    async function persistDraftAfterStateSettles(viewId: string, identity: SavedViewDraftIdentity, draftKey: string): Promise<void> {
        await tick();

        const view = activeSavedView;
        if (!view || view.id !== viewId || hydratedSavedViewId !== view.id || appliedDraftKey !== draftKey) {
            return;
        }

        const draft = buildSavedViewDraft(view);
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

        // Subscribe to every field that contributes to dirty state, then persist after
        // route-level filter normalization has settled for this render cycle.
        void isModified;
        void persistDraftAfterStateSettles(view.id, identity, draftKey);
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
        applyColumnState(view);
        applyDisplayState(view);
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
