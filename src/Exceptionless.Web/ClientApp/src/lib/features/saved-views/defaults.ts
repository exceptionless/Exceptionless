import { resolve } from '$app/paths';

import type { SavedView, ViewSavedViewDefaults } from './models';

import { savedViewHref } from './slugs';

export function getSavedViewDefaultHref(defaults: null | undefined | ViewSavedViewDefaults, stackSavedViews: null | SavedView[] | undefined = []): string {
    const savedView = defaults?.user_default ?? defaults?.organization_default ?? stackSavedViews?.[0];
    return savedView ? savedViewHref(savedView) : resolve('/(app)/stack');
}
