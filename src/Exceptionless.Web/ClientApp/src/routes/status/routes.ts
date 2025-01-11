import Activity from 'lucide-svelte/icons/activity';

import type { NavigationItem } from '../routes';

export const routes: NavigationItem[] = [
    {
        group: 'status',
        href: '/next/status',
        icon: Activity,
        show: () => false,
        title: 'Service Status'
    }
];
