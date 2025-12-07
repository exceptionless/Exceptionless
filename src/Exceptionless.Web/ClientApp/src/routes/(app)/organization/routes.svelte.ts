import { resolve } from '$app/paths';
import Settings from '@lucide/svelte/icons/settings';

import type { NavigationItem } from '../../routes.svelte';

import { routes as organizationSettingsRoutes } from './[organizationId]/routes.svelte';

export function routes(): NavigationItem[] {
    return [
        {
            group: 'Settings',
            href: resolve('/(app)/organization/list'),
            icon: Settings,
            show: (context) => !context.impersonating,
            title: 'Organizations'
        },
        ...organizationSettingsRoutes()
    ];
}
