import type { IFilter } from '$comp/faceted-filter';
import type { ColumnVisibilityState } from '@tanstack/svelte-table';

import { deserializeFilters } from '$features/events/components/filters/helpers.svelte';
import { getOrganizationQuery } from '$features/organizations/api.svelte';
import { organization } from '$features/organizations/context.svelte';
import { untrack } from 'svelte';

import type { SavedView } from './models';

import { getSavedViewsByViewQuery } from './api.svelte';

const SAVED_VIEWS_FEATURE = 'feature-saved-views';

export interface SavedViewQueryParams {
    filter: null | string;
    saved: null | string | undefined;
    sort?: null | string;
    time?: null | string;
}

export interface UseSavedViewsOptions {
    filterCacheKey: (filter: null | string) => string;
    getColumnVisibility?: () => ColumnVisibilityState;
    getFilterDefinitions?: () => string;
    queryParams: SavedViewQueryParams;
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
    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return organization.current;
            }
        }
    });

    // Feature flag gate: only enable saved views if the organization has the feature
    const isEnabled = $derived(organizationQuery.data?.features?.includes(SAVED_VIEWS_FEATURE) ?? false);

    // Some routes, such as stream, do not declare every saved-view query parameter.
    const supportsSort = supportsSortQueryParam(options.queryParams);
    const supportsTime = supportsTimeQueryParam(options.queryParams);

    const savedViewsListQuery = getSavedViewsByViewQuery({
        route: {
            get organizationId() {
                return isEnabled ? organization.current : undefined;
            },
            get view() {
                return options.view;
            }
        }
    });

    const activeSavedView = $derived(savedViewsListQuery.data?.find((v) => v.id === options.queryParams.saved));

    // Hydrate filters/columns when a saved view loads, or clear params if the view is no longer found.
    // lastLoadedViewId prevents re-hydration on background refetches (which would stomp user edits).
    let lastLoadedViewId = '';
    let hasAutoRestored = false;
    let lastAutoRestoredOrganizationId = '';
    $effect(() => {
        const savedId = options.queryParams.saved;
        const isLoading = savedViewsListQuery.isLoading;
        const isFetching = savedViewsListQuery.isFetching;
        const views = savedViewsListQuery.data;

        if (!savedId || isLoading || !views) {
            if (!savedId) {
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
            hasAutoRestored = false;
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

        if (view.columns && options.setColumnVisibility) {
            options.setColumnVisibility(view.columns);
        }
    });

    // Auto-load default saved view when navigating to page without explicit params
    $effect(() => {
        const organizationId = organization.current;
        const views = savedViewsListQuery.data;
        if (!organizationId) {
            return;
        }

        if (organizationId !== lastAutoRestoredOrganizationId) {
            hasAutoRestored = false;
            lastAutoRestoredOrganizationId = organizationId;
        }

        if (hasAutoRestored) {
            return;
        }

        // Don't lock out the auto-restore while the feature flag is still resolving
        // or the views list hasn't loaded yet. Without this guard the effect fires
        // while the query is disabled (isLoading=false but data=undefined) and marks
        // hasAutoRestored=true before any view data is available.
        if (!isEnabled || savedViewsListQuery.isLoading) {
            return;
        }

        hasAutoRestored = true;

        const search = window.location.search;
        const hasExplicitParams =
            /[?&]saved(?:[=&]|$)/.test(search) || /[?&]filter(?:[=&]|$)/.test(search) || /[?&]sort(?:[=&]|$)/.test(search) || /[?&]time(?:[=&]|$)/.test(search);
        if (hasExplicitParams) {
            return;
        }

        untrack(() => {
            const defaultView = views?.find((v) => v.is_default);
            if (defaultView) {
                options.queryParams.saved = defaultView.id;
            }
        });
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

        if (options.getColumnVisibility && !columnsEqual(options.getColumnVisibility(), view.columns)) {
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
        if (view.columns && options.setColumnVisibility) {
            options.setColumnVisibility(view.columns);
        }
    }

    function handleClearSavedView() {
        options.queryParams.saved = null;
        options.queryParams.filter = null;
        setSortQueryParam(options.queryParams, null);
        setTimeQueryParam(options.queryParams, null);
        if (options.setColumnVisibility) {
            options.setColumnVisibility({});
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
        get isModified() {
            return isModified;
        },
        get savedViews() {
            return savedViewsListQuery.data ?? [];
        }
    };
}

function columnsEqual(a: ColumnVisibilityState | undefined, b: null | Record<string, boolean> | undefined): boolean {
    const aEntries = Object.entries(a ?? {}).sort(([k1], [k2]) => k1.localeCompare(k2));
    const bEntries = Object.entries(b ?? {}).sort(([k1], [k2]) => k1.localeCompare(k2));

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
