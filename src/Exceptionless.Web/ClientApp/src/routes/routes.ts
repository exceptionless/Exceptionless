import type { User } from '$lib/models/api';
import type { Component } from 'svelte';

import { routes as appRoutes } from './(app)/routes';
import { routes as authRoutes } from './(auth)/routes';

export type NavigationItemContext = {
    authenticated: boolean;
    user?: User;
};

export type NavigationItem = {
    group: string;
    href: string;
    icon: Component;
    show?: (context: NavigationItemContext) => boolean;
    title: string;
};

export const routes: NavigationItem[] = [...appRoutes, ...authRoutes];
