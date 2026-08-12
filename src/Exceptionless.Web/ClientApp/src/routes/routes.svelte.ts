import type { ResolvedPathname } from '$app/types';
import type { ShortcutKey } from '$features/shared/keyboard-shortcuts';
import type { ViewCurrentUser } from '$features/users/models';
import type { Icon } from '@lucide/svelte';
import type { Component } from 'svelte';

import { routes as appRoutes } from './(app)/routes.svelte';
import { routes as authRoutes } from './(auth)/routes.svelte';

export type NavigationChild = {
    href: string;
    title: string;
    usesPremiumFeatures?: boolean;
};

export type NavigationItem = {
    children?: NavigationChild[];
    group: string;
    href: ResolvedPathname | string;
    icon: Component | typeof Icon;
    openInNewTab?: boolean;
    shortcut?: readonly ShortcutKey[];
    show?: (context: NavigationItemContext) => boolean;
    title: string;
    view?: string;
};

export type NavigationItemContext = {
    authenticated: boolean;
    impersonating?: boolean;
    user?: ViewCurrentUser;
};

export function routes(): NavigationItem[] {
    return [...appRoutes(), ...authRoutes()];
}
