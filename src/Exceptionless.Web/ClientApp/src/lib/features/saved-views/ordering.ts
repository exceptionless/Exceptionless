import type { UserSavedViewOrderPreference } from '$generated/api';

import type { SavedView } from './models';

export function getPersonalSavedViewOrder(
    savedViewOrders: null | undefined | UserSavedViewOrderPreference[],
    organizationId: string | undefined,
    viewType: string
): string[] {
    if (!organizationId) {
        return [];
    }

    const orderedIds: string[] = [];
    for (const preference of savedViewOrders ?? []) {
        if (preference.organization_id !== organizationId || preference.view_type !== viewType) {
            continue;
        }

        for (const savedViewId of preference.saved_view_ids) {
            if (!orderedIds.includes(savedViewId)) {
                orderedIds.push(savedViewId);
            }
        }
    }

    return orderedIds;
}

export function resolvePersonalSavedViewOrder(savedViews: SavedView[], orderedIds: null | string[] | undefined): SavedView[] {
    const savedViewsById = new Map(savedViews.map((savedView) => [savedView.id, savedView]));
    const resolved: SavedView[] = [];
    const resolvedIds = new Set<string>();

    for (const savedViewId of orderedIds ?? []) {
        const savedView = savedViewsById.get(savedViewId);
        if (!savedView || resolvedIds.has(savedView.id)) {
            continue;
        }

        resolved.push(savedView);
        resolvedIds.add(savedView.id);
    }

    const unordered = savedViews
        .filter((savedView) => !resolvedIds.has(savedView.id))
        .sort((left, right) => left.name.localeCompare(right.name) || left.id.localeCompare(right.id));

    return [...resolved, ...unordered];
}
