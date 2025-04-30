import Settings from '@lucide/svelte/icons/settings';

import type { NavigationItem } from '../../routes.svelte';

import { routes as projectSettingsRoutes } from './[projectId]/routes.svelte';

export function routes(): NavigationItem[] {
    return [
        {
            group: 'Settings',
            href: '/next/project/list',
            icon: Settings,
            title: 'Projects'
        },
        ...projectSettingsRoutes()
    ];
}
