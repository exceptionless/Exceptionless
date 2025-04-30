import Activity from '@lucide/svelte/icons/activity';

import type { NavigationItem } from '../routes.svelte';

export function routes(): NavigationItem[] {
    return [
        {
            group: 'status',
            href: '/next/status',
            icon: Activity,
            show: () => false,
            title: 'Service Status'
        }
    ];
}
