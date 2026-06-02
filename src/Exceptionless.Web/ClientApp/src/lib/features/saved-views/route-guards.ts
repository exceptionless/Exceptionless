import { browser } from '$app/environment';
import { error } from '@sveltejs/kit';

import type { SavedView } from './models';

type SavedViewRoute = 'events' | 'stacks';

export async function requireSavedViewSlug(fetch: typeof globalThis.fetch, view: SavedViewRoute, slug: string): Promise<void> {
    if (!browser) {
        return;
    }

    const accessToken = getStoredString('satellizer_token');
    const organizationId = getStoredString('organization');
    if (!accessToken || !organizationId) {
        return;
    }

    const response = await fetch(`/api/v2/organizations/${organizationId}/saved-views/${view}`, {
        headers: {
            Authorization: `Bearer ${accessToken}`
        }
    });

    if (!response.ok) {
        return;
    }

    const savedViews = (await response.json()) as SavedView[];
    if (!savedViews.some((savedView) => savedView.slug === slug)) {
        throw error(404, `The saved ${view === 'events' ? 'Events' : 'Stacks'} view "${slug}" could not be found.`);
    }
}

function getStoredString(key: string): string | undefined {
    const value = localStorage.getItem(key);
    if (!value) {
        return undefined;
    }

    try {
        const parsed = JSON.parse(value) as unknown;
        return typeof parsed === 'string' ? parsed : value;
    } catch {
        return value;
    }
}
