import IconLogin from '~icons/mdi/login';
import IconLogout from '~icons/mdi/logout';

import type { NavigationItem, NavigationItemContext } from '../routes';

export const routes: NavigationItem[] = [
    {
        group: 'Session',
        href: '/next/login',
        icon: IconLogin,
        show: (context: NavigationItemContext) => !context.authenticated,
        title: 'Log in'
    },
    {
        group: 'Session',
        href: '/next/logout',
        icon: IconLogout,
        show: (context: NavigationItemContext) => context.authenticated,
        title: 'Log out'
    }
];
