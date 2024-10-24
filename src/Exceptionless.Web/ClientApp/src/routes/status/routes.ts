import StatusLogin from '~icons/mdi/status';

import type { NavigationItem } from '../routes';

export const routes: NavigationItem[] = [
    {
        group: 'status',
        href: '/next/status',
        icon: StatusLogin,
        show: () => false,
        title: 'Service Status'
    }
];
