import type { User } from '$features/users/models';
import type { Icon } from 'lucide-svelte';
import type { Component } from 'svelte';

import { routes as appRoutes } from './(app)/routes';
import { routes as authRoutes } from './(auth)/routes';

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

export const routes: NavigationItem[] = [...appRoutes, ...authRoutes];
