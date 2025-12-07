import { resolve } from '$app/paths';
import LogIn from '@lucide/svelte/icons/log-in';
import LogOut from '@lucide/svelte/icons/log-out';

import type { NavigationItem, NavigationItemContext } from '../routes.svelte';

export function routes(): NavigationItem[] {
    return [
        {
            group: 'Session',
            href: resolve('/(auth)/login'),
            icon: LogIn,
            show: (context: NavigationItemContext) => !context.authenticated,
            title: 'Log in'
        },
        {
            group: 'Session',
            href: resolve('/(auth)/logout'),
            icon: LogOut,
            show: (context: NavigationItemContext) => context.authenticated,
            title: 'Log out'
        }
    ];
}
