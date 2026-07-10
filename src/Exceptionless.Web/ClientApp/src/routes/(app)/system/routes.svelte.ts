import { resolve } from '$app/paths';
import Bell from '@lucide/svelte/icons/bell';
import Bookmark from '@lucide/svelte/icons/bookmark';
import Database from '@lucide/svelte/icons/database';
import DatabaseZap from '@lucide/svelte/icons/database-zap';
import KeyRound from '@lucide/svelte/icons/key-round';
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
            href: resolve('/(app)/system/notifications'),
            icon: Bell,
            show: (context) => context.user?.roles?.includes('global') ?? false,
            title: 'Notifications'
        },
        {
            group: 'System',
            href: resolve('/(app)/system/saved-views'),
            icon: Bookmark,
            show: (context) => context.user?.roles?.includes('global') ?? false,
            title: 'Saved Views'
        },
        {
            group: 'System',
            href: resolve('/(app)/system/oauth-applications'),
            icon: KeyRound,
            show: (context) => context.user?.roles?.includes('global') ?? false,
            title: 'OAuth Apps'
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
