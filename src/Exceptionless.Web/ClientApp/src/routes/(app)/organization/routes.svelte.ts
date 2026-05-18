import { resolve } from '$app/paths';
import Building2 from '@lucide/svelte/icons/building-2';

import type { NavigationItem } from '../../routes.svelte';

import { routes as organizationSettingsRoutes } from './[organizationId]/routes.svelte';

export function routes(): NavigationItem[] {
    return [
        {
            group: 'Settings',
            href: resolve('/(app)/organization/list'),
            icon: Building2,
            show: (context) => !context.impersonating,
            title: 'Organizations'
        },
        ...organizationSettingsRoutes()
    ];
}
