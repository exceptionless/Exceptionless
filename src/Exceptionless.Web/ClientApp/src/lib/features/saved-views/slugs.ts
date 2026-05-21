import { resolve } from '$app/paths';

import type { SavedView } from './models';

type SavedViewIdentity = Pick<SavedView, 'id' | 'name'> & { slug?: null | string };
type SavedViewLink = Pick<SavedView, 'id' | 'name' | 'view_type'> & { slug?: null | string };

export const SAVED_VIEW_NAME_MAX_LENGTH = 100;
export const SAVED_VIEW_SLUG_MAX_LENGTH = 100;
export const SAVED_VIEW_SLUG_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
export const SAVED_VIEW_OBJECT_ID_PATTERN = /^[a-f0-9]{24}$/;

export function findSavedViewByName(savedViews: SavedViewIdentity[], name: string, excludingId?: string): SavedViewIdentity | undefined {
    const normalizedName = name.trim().toLowerCase();
    if (!normalizedName) {
        return undefined;
    }

    return savedViews.find((savedView) => savedView.id !== excludingId && savedView.name.trim().toLowerCase() === normalizedName);
}

export function findSavedViewBySlug(savedViews: SavedViewIdentity[], slug: string, excludingId?: string): SavedViewIdentity | undefined {
    const normalizedSlug = savedViewSlug(slug);
    if (!normalizedSlug) {
        return undefined;
    }

    return savedViews.find((savedView) => savedView.id !== excludingId && savedViewResolvedSlug(savedView) === normalizedSlug);
}

export function isSavedViewSlugReserved(slug: string): boolean {
    return SAVED_VIEW_OBJECT_ID_PATTERN.test(slug);
}

export function isSavedViewSlugValid(slug: string): boolean {
    return SAVED_VIEW_SLUG_PATTERN.test(slug) && !isSavedViewSlugReserved(slug);
}

export function savedViewHref(savedView: SavedViewLink): string {
    if (savedView.view_type === 'stream') {
        return `${resolve('/(app)/stream')}?saved=${savedView.id}`;
    }

    const base = savedView.view_type === 'events' ? resolve('/(app)/events') : resolve('/(app)/issues');
    return `${base}/${savedViewResolvedSlug(savedView)}`;
}

export function savedViewResolvedSlug(savedView: Pick<SavedViewLink, 'name' | 'slug'>): string {
    return savedView.slug || savedViewSlug(savedView.name);
}

export function savedViewSlug(value: string): string {
    return value
        .trim()
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, '-')
        .replace(/^-+|-+$/g, '')
        .replace(/-+/g, '-');
}
