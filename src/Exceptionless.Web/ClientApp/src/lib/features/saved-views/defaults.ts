import type { UserOrganizationPreference } from '$generated/api';

import { resolve } from '$app/paths';

import type { SavedView } from './models';

import { savedViewHref } from './slugs';

export interface ResolvedSavedViewDefaults {
    organizationDefault?: SavedView;
    userDefault?: SavedView;
}

interface ResolveSavedViewDefaultsOptions {
    organizationDefaultSavedViewId?: null | string;
    organizationId?: string;
    organizationPreferences?: null | UserOrganizationPreference[];
    savedViews?: null | SavedView[];
}

export function getSavedViewDefaultHref(defaults: ResolvedSavedViewDefaults, savedViews: null | SavedView[] | undefined = []): string {
    const savedView = defaults.userDefault ?? defaults.organizationDefault ?? savedViews?.find((savedView) => savedView.view_type === 'stacks');
    return savedView ? savedViewHref(savedView) : resolve('/(app)/stack');
}

export function resolveSavedViewDefaults(options: ResolveSavedViewDefaultsOptions): ResolvedSavedViewDefaults {
    const savedViews = (options.savedViews ?? []).filter((savedView) => savedView.organization_id === options.organizationId);
    const savedViewsById = new Map(savedViews.map((savedView) => [savedView.id, savedView]));
    const userDefaultIds = [
        ...new Set(
            (options.organizationPreferences ?? [])
                .filter((preference) => preference.organization_id === options.organizationId)
                .map((preference) => preference.default_saved_view_id)
                .filter((savedViewId): savedViewId is string => !!savedViewId)
        )
    ].sort();
    const userDefault = userDefaultIds.map((savedViewId) => savedViewsById.get(savedViewId)).find((savedView) => !!savedView);
    const organizationDefault = options.organizationDefaultSavedViewId ? savedViewsById.get(options.organizationDefaultSavedViewId) : undefined;

    return {
        organizationDefault: organizationDefault?.user_id ? undefined : organizationDefault,
        userDefault
    };
}
