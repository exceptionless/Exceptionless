import type { User } from '$features/users/models';
import type { Icon } from '@lucide/svelte';
import type { Component } from 'svelte';

import { routes as appRoutes } from './(app)/routes.svelte';
import { routes as authRoutes } from './(auth)/routes.svelte';

export type NavigationItem = {
    group: string;
    href: string;
    icon: Component | typeof Icon;
    openInNewTab?: boolean;
    show?: (context: NavigationItemContext) => boolean;
    title: string;
};

export type NavigationItemContext = {
    authenticated: boolean;
    user?: User;
};

export function routes(): NavigationItem[] {
    return [...appRoutes(), ...authRoutes()];
}
