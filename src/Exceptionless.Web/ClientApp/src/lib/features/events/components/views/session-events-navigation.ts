import type { SavedView } from '$features/saved-views/models';

import { resolve } from '$app/paths';
import { savedViewHref, savedViewResolvedSlug } from '$features/saved-views/slugs';

export function getSessionEventsPath(savedViews: SavedView[] | undefined, isPending = false): string | undefined {
    if (isPending) {
        return undefined;
    }

    const sharedEventViews = (savedViews ?? []).filter((savedView) => savedView.view_type === 'events' && !savedView.user_id);
    const allEventsView =
        sharedEventViews.find((savedView) => savedView.name.trim().toLowerCase() === 'all') ??
        sharedEventViews.find((savedView) => /^all(?:-\d+)?$/.test(savedViewResolvedSlug(savedView)));

    return allEventsView ? savedViewHref(allEventsView) : resolve('/(app)/event');
}
