import type { IFilter } from '$comp/faceted-filter';
import type { ColumnOrderState, ColumnVisibilityState } from '@tanstack/svelte-table';

import { deserializeFilters } from '$features/events/components/filters/helpers.svelte';
import { organization } from '$features/organizations/context.svelte';
import { untrack } from 'svelte';

import type { SavedView } from './models';

import { getSavedViewsByViewQuery } from './api.svelte';

export interface SavedViewQueryParams {
    filter: null | string;
    saved: null | string | undefined;
    sort?: null | string;
    time?: null | string;
}

export interface UseSavedViewsOptions {
    defaultColumnVisibility?: ColumnVisibilityState;
    filterCacheKey: (filter: null | string) => string;
    getColumnOrder?: () => ColumnOrderState;
    getColumnVisibility?: () => ColumnVisibilityState;
    getFilterDefinitions?: () => string;
    queryParams: SavedViewQueryParams;
    setColumnOrder?: (order: ColumnOrderState) => void;
    setColumnVisibility?: (visibility: ColumnVisibilityState) => void;
    updateFilterCache: (key: string, filters: IFilter[]) => void;
    view: string;
}

export interface UseSavedViewsReturn {
    activeSavedView: SavedView | undefined;
    handleClearSavedView: () => void;
    handleLoadView: (id: string) => void;
    handleResetToSaved: () => void;
    isEnabled: boolean;
    isLoading: boolean;
    isModified: boolean;
    savedViews: SavedView[];
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

    const activeSavedView = $derived(savedViewsListQuery.data?.find((v) => v.id === options.queryParams.saved));

    function applyColumnState(view: Pick<SavedView, 'column_order' | 'columns'> | undefined): void {
        if (options.setColumnVisibility) {
            options.setColumnVisibility(view?.columns ?? {});
        }

        if (options.setColumnOrder) {
            options.setColumnOrder(view?.column_order ?? []);
        }
    }

    // Hydrate filters/columns when a saved view loads, or clear params if the view is no longer found.
    // lastLoadedViewId prevents re-hydration on background refetches (which would stomp user edits).
    let lastLoadedViewId: string | undefined;
    $effect(() => {
        const savedId = options.queryParams.saved;
        const isLoading = savedViewsListQuery.isLoading;
        const isFetching = savedViewsListQuery.isFetching;
        const views = savedViewsListQuery.data;

        if (!savedId || isLoading || !views) {
            if (!savedId) {
                if (lastLoadedViewId !== '') {
                    applyColumnState(undefined);
                }

                lastLoadedViewId = '';
            }

            return;
        }

        const view = views.find((v) => v.id === savedId);
        if (!view) {
            // Skip while refetching to avoid false-positive clears during cache invalidation
            if (isFetching) {
                return;
            }

            // View not found after a definitive load — clear params and allow auto-restore to re-run
            untrack(() => {
                options.queryParams.saved = null;
            });
            options.queryParams.filter = null;
            setSortQueryParam(options.queryParams, null);
            setTimeQueryParam(options.queryParams, null);
            return;
        }

        // Already loaded this view — skip to avoid stomping user edits on background refetch
        if (savedId === lastLoadedViewId) {
            return;
        }

        lastLoadedViewId = savedId;

        if (view.filter_definitions) {
            try {
                const hydrated = deserializeFilters(view.filter_definitions);
                options.updateFilterCache(options.filterCacheKey(view.filter ?? null), hydrated);
            } catch {
                console.error('Failed to deserialize saved view filter definitions');
            }
        }

        options.queryParams.filter = view.filter ?? null;
        setSortQueryParam(options.queryParams, view.sort ?? null);
        setTimeQueryParam(options.queryParams, view.time ?? null);
        applyColumnState(view);
    });

    // Detect if current filters or columns differ from the active saved view
    const isModified = $derived.by(() => {
        const view = activeSavedView;
        if (!view || !options.queryParams.saved) {
            return false;
        }

        if ((options.queryParams.filter ?? null) !== (view.filter ?? null)) {
            return true;
        }

        if (supportsTime && (options.queryParams.time ?? null) !== (view.time ?? null)) {
            return true;
        }

        if (supportsSort && (options.queryParams.sort ?? null) !== (view.sort ?? null)) {
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

        return false;
    });

    function handleLoadView(id: string) {
        options.queryParams.saved = id;
    }

    function handleResetToSaved() {
        const view = activeSavedView;
        if (!view) {
            return;
        }

        if (view.filter_definitions) {
            try {
                const hydrated = deserializeFilters(view.filter_definitions);
                options.updateFilterCache(options.filterCacheKey(view.filter ?? null), hydrated);
            } catch {
                console.error('Failed to deserialize saved view filter definitions');
            }
        }

        options.queryParams.filter = view.filter ?? null;
        setSortQueryParam(options.queryParams, view.sort ?? null);
        setTimeQueryParam(options.queryParams, view.time ?? null);
        applyColumnState(view);
    }

    function handleClearSavedView() {
        options.queryParams.saved = null;
        options.queryParams.filter = null;
        setSortQueryParam(options.queryParams, null);
        setTimeQueryParam(options.queryParams, null);
        applyColumnState(undefined);
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

function filterDefinitionsEqual(a: null | string | undefined, b: null | string | undefined): boolean {
    return normalizeFilterDefinitions(a) === normalizeFilterDefinitions(b);
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

        return JSON.stringify(parsed);
    } catch {
        return value;
    }
}
