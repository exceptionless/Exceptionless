import Settings from '@lucide/svelte/icons/settings';

import type { NavigationItem } from '../../routes.svelte';

import { routes as organizationSettingsRoutes } from './[organizationId]/routes.svelte';

export function routes(): NavigationItem[] {
    return [
        {
            group: 'Settings',
            href: '/next/organization/list',
            icon: Settings,
            title: 'Organizations'
        },
        ...organizationSettingsRoutes()
    ];
}
