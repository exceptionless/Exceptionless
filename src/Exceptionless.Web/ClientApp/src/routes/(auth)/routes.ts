import LogIn from 'lucide-svelte/icons/log-in';
import LogOut from 'lucide-svelte/icons/log-out';

import type { NavigationItem, NavigationItemContext } from '../routes';

export const routes: NavigationItem[] = [
    {
        group: 'Session',
        href: '/next/login',
        icon: LogIn,
        show: (context: NavigationItemContext) => !context.authenticated,
        title: 'Log in'
    },
    {
        group: 'Session',
        href: '/next/logout',
        icon: LogOut,
        show: (context: NavigationItemContext) => context.authenticated,
        title: 'Log out'
    }
];
