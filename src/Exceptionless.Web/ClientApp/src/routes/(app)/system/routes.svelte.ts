import { resolve } from '$app/paths';
import Database from '@lucide/svelte/icons/database';
import DatabaseZap from '@lucide/svelte/icons/database-zap';
import LayoutDashboard from '@lucide/svelte/icons/layout-dashboard';
import Play from '@lucide/svelte/icons/play';

import type { NavigationItem } from '../../routes.svelte';

export function routes(): NavigationItem[] {
    return [
        {
            group: 'System',
            href: resolve('/(app)/system/overview'),
            icon: LayoutDashboard,
            show: (context) => context.user?.roles?.includes('global') ?? false,
            title: 'Overview'
        },
        {
            group: 'System',
            href: resolve('/(app)/system/elasticsearch/overview'),
            icon: Database,
            show: (context) => context.user?.roles?.includes('global') ?? false,
            title: 'Elasticsearch'
        },
        {
            group: 'System',
            href: resolve('/(app)/system/actions'),
            icon: Play,
            show: (context) => context.user?.roles?.includes('global') ?? false,
            title: 'Actions'
        },
        {
            group: 'System',
            href: resolve('/(app)/system/migrations'),
            icon: DatabaseZap,
            show: (context) => context.user?.roles?.includes('global') ?? false,
            title: 'Migrations'
        }
    ];
}
