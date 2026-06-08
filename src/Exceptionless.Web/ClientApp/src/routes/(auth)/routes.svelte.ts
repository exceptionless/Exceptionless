import { resolve } from '$app/paths';
import LogIn from '@lucide/svelte/icons/log-in';

import type { NavigationItem, NavigationItemContext } from '../routes.svelte';

export function routes(): NavigationItem[] {
    return [
        {
            group: 'Session',
            href: resolve('/(auth)/login'),
            icon: LogIn,
            show: (context: NavigationItemContext) => !context.authenticated,
            title: 'Log in'
        }
    ];
}
