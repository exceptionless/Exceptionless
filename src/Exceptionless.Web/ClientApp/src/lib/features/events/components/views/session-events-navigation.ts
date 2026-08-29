import type { SavedView } from '$features/saved-views/models';

import { resolve } from '$app/paths';
import { savedViewHref, savedViewResolvedSlug } from '$features/saved-views/slugs';

export function getSessionEventsPath(savedViews: SavedView[] | undefined): string {
    const eventViews = (savedViews ?? []).filter((savedView) => savedView.view_type === 'events');
    const allEventsView = eventViews.find((savedView) => savedViewResolvedSlug(savedView) === 'all');

    return allEventsView ? savedViewHref(allEventsView) : resolve('/(app)/event');
}
