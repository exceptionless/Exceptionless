import Settings from '@lucide/svelte/icons/settings';

import type { NavigationItem } from '../../routes.svelte';

import { routes as projectSettingsRoutes } from './[projectId]/routes.svelte';

export function routes(): NavigationItem[] {
    return [
        {
            group: 'Projects',
            href: '/next/project/add',
            icon: Settings,
            title: 'Add Project'
        },
        ...projectSettingsRoutes()
    ];
}
