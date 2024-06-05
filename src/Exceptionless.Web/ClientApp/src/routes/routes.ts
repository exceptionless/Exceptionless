import type { Component } from 'svelte';
import type { User } from '$lib/models/api';
import { routes as appRoutes } from './(app)/routes';
import { routes as authRoutes } from './(auth)/routes';

export type NavigationItemContext = {
    authenticated: boolean;
    user?: User;
};

export type NavigationItem = {
    group: string;
    title: string;
    href: string;
    icon: Component;
    show?: (context: NavigationItemContext) => boolean;
};

export const routes: NavigationItem[] = [...appRoutes, ...authRoutes];
