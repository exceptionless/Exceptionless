import { resolve } from '$app/paths';
import Folder from '@lucide/svelte/icons/folder';
import Settings from '@lucide/svelte/icons/settings';

import type { NavigationItem } from '../../routes.svelte';

import { routes as projectSettingsRoutes } from './[projectId]/routes.svelte';

export function routes(): NavigationItem[] {
    return [
        {
            group: 'Settings',
            href: resolve('/(app)/project/list'),
            icon: Folder,
            title: 'Projects'
        },
        {
            group: 'Projects',
            href: resolve('/(app)/project/add'),
            icon: Settings,
            title: 'Add Project'
        },
        ...projectSettingsRoutes()
    ];
}
