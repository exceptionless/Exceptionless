import type { SavedView } from '$features/saved-views/models';

import { resolve } from '$app/paths';
import { savedViewHref } from '$features/saved-views/slugs';

const RESET_FILTER_QUERY_PARAMS = ['bot', 'filter', 'first', 'level', 'project', 'reference', 'stack', 'status', 'tag', 'type', 'version'] as const;

export function getSessionEventsHref(eventsPath: string | undefined, sessionId: string | undefined): string | undefined {
    if (!eventsPath || !sessionId) {
        return undefined;
    }

    const query = new URLSearchParams({ session: sessionId, time: 'all' });
    for (const name of RESET_FILTER_QUERY_PARAMS) {
        query.set(name, '');
    }

    return `${eventsPath}?${query.toString()}`;
}

export function getSessionEventsPath(savedViews: SavedView[] | undefined, isPending = false): string | undefined {
    if (isPending) {
        return undefined;
    }

    const sharedEventViews = (savedViews ?? []).filter((savedView) => savedView.view_type === 'events' && !savedView.user_id);
    const allEventsView = sharedEventViews.find((savedView) => savedView.name.trim().toLowerCase() === 'all');

    return allEventsView ? savedViewHref(allEventsView) : resolve('/(app)/event');
}
