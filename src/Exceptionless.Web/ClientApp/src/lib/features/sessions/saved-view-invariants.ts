import type { IFilter } from '$comp/faceted-filter';
import type { SavedView } from '$features/saved-views/models';

import { deserializeFilters, serializeFilters, toFilter } from '$features/events/components/filters/helpers.svelte';

export const SESSION_EVENT_FILTER = 'type:session';

export function getSessionQueryFilter(filter: null | string | undefined): string {
    return filter ? `${SESSION_EVENT_FILTER} AND (${filter})` : SESSION_EVENT_FILTER;
}

export function getSessionViewFilters(filters: IFilter[]): IFilter[] {
    return filters.filter((filter) => filter.type !== 'type');
}

export function normalizeSessionSavedView(view: SavedView): SavedView {
    if (view.filter_definitions) {
        const filters = getSessionViewFilters(deserializeFilters(view.filter_definitions));
        const filterDefinitions = serializeFilters(filters);
        const filter = toFilter(filters.filter((candidate) => candidate.type !== 'date')) || null;

        if (view.filter === filter && view.filter_definitions === filterDefinitions) {
            return view;
        }

        return {
            ...view,
            filter,
            filter_definitions: filterDefinitions
        };
    }

    if (view.filter?.trim().toLowerCase() !== SESSION_EVENT_FILTER) {
        return view;
    }

    return {
        ...view,
        filter: null
    };
}
