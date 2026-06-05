import type { IFilter } from '$comp/faceted-filter';
import type { ColumnOrderState, ColumnVisibilityState } from '@tanstack/svelte-table';

import { goto } from '$app/navigation';
import { buildFilterCacheKey, deserializeFilters, serializeFilters } from '$features/events/components/filters/helpers.svelte';
import { organization } from '$features/organizations/context.svelte';

import type { SavedView } from './models';

import { getSavedViewsByViewQuery } from './api.svelte';
import { savedViewHref, savedViewResolvedSlug } from './slugs';

export interface SavedViewQueryParams {
    filter: null | string | undefined;
    filters?: null | string | undefined;
    saved?: null | string | undefined;
    sort?: null | string;
    time?: null | string;
}

export interface UseSavedViewsOptions {
    baseHref?: string;
    defaultColumnVisibility?: ColumnVisibilityState;
    defaultFilter?: null | string;
    defaultTime?: null | string;
    filterCacheKey: (filter: null | string) => string;
    getColumnOrder?: () => ColumnOrderState;
    getColumnVisibility?: () => ColumnVisibilityState;
    getFilter?: () => null | string;
    getFilterDefinitions?: () => string;
    getShowChart?: () => boolean;
    getShowStats?: () => boolean;
    getSort?: () => null | string | undefined;
    getTime?: () => null | string | undefined;
    queryParams: SavedViewQueryParams;
    setColumnOrder?: (order: ColumnOrderState) => void;
    setColumnVisibility?: (visibility: ColumnVisibilityState) => void;
    setShowChart?: (show: boolean) => void;
    setShowStats?: (show: boolean) => void;
    slug?: string;
    updateFilterCache: (key: string, filters: IFilter[]) => void;
    view: string;
}

export interface UseSavedViewsReturn {
    activeSavedView: SavedView | undefined;
    handleClearSavedView: () => void;
    handleLoadView: (view: SavedView) => void;
    handleResetToSaved: () => void;
    isEnabled: boolean;
    isLoading: boolean;
    isMissing: boolean;
    isModified: boolean;
    savedViews: SavedView[];
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

    function applyColumnState(view: Pick<SavedView, 'column_order' | 'columns'> | undefined): void {
        if (options.setColumnVisibility) {
            options.setColumnVisibility(view?.columns ?? {});
        }

        if (options.setColumnOrder) {
            options.setColumnOrder(view?.column_order ?? []);
        }
    }

    function applyDisplayState(view: Pick<SavedView, 'show_chart' | 'show_stats'> | undefined): void {
        options.setShowStats?.(view?.show_stats ?? true);
        options.setShowChart?.(view?.show_chart ?? true);
    }

    // Hydrate saved view state when a saved view loads. Query params remain URL overrides.
    // lastLoadedViewId prevents re-hydration on background refetches (which would stomp user edits).
    let lastLoadedViewId = '';
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
            }

            return;
        }

        if (!view) {
            // Skip while refetching to avoid false-positive clears during cache invalidation
            if (isFetching) {
                return;
            }

            return;
        }

        // Already loaded this view — skip to avoid stomping user edits on background refetch
        if (view.id === lastLoadedViewId) {
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

        if (options.getColumnVisibility && !columnsEqual(options.getColumnVisibility(), view.columns, options.defaultColumnVisibility)) {
            return true;
        }

        if (options.getColumnOrder && !columnOrderEqual(options.getColumnOrder(), view.column_order)) {
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
        options.queryParams.filter = null;
        options.queryParams.filters = null;
        setSortQueryParam(options.queryParams, null);
        setTimeQueryParam(options.queryParams, null);
        applyColumnState(undefined);
        applyDisplayState(undefined);

        if (supportsSavedQueryParam(options.queryParams)) {
            options.queryParams.saved = undefined;
        }

        if (options.baseHref) {
            goto(options.baseHref);
        }
    }

    return {
        get activeSavedView() {
            return activeSavedView;
        },
        handleClearSavedView,
        handleLoadView,
        handleResetToSaved,
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
        }
    };
}

function columnOrderEqual(a: ColumnOrderState | undefined, b: null | string[] | undefined): boolean {
    const normalize = (value: null | string[] | undefined) => (value ?? []).filter((columnId) => columnId !== 'select');
    const aOrder = normalize(a);
    const bOrder = normalize(b);

    if (aOrder.length !== bOrder.length) {
        return false;
    }

    return aOrder.every((columnId, index) => columnId === bOrder[index]);
}

function columnsEqual(
    a: ColumnVisibilityState | undefined,
    b: null | Record<string, boolean> | undefined,
    defaultColumnVisibility: ColumnVisibilityState = {}
): boolean {
    const normalize = (value: ColumnVisibilityState | null | undefined) => ({ ...defaultColumnVisibility, ...(value ?? {}) });
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

function normalizeFilterDefinitions(value: null | string | undefined): string {
    if (!value) {
        return '[]';
    }

    try {
        const parsed = JSON.parse(value);
        if (!Array.isArray(parsed) || parsed.length === 0) {
            return '[]';
        }

        return serializeFilters(deserializeFilters(value));
    } catch {
        return value;
    }
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
