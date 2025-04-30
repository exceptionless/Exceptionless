import Documentation from '@lucide/svelte/icons/book-open';
import ApiDocumentations from '@lucide/svelte/icons/braces';
import Issues from '@lucide/svelte/icons/bug';
import EventStream from '@lucide/svelte/icons/calendar-arrow-down';
import Events from '@lucide/svelte/icons/calendar-days';
import Support from '@lucide/svelte/icons/circle-help';
import GitHub from '@lucide/svelte/icons/github';

import type { NavigationItem } from '../routes.svelte';

import { routes as accountRoutes } from './account/routes.svelte';
import { routes as eventRoutes } from './event/routes.svelte';
import { routes as projectRoutes } from './project/routes.svelte';

export function routes(): NavigationItem[] {
    const items = [
        {
            group: 'Dashboards',
            href: '/next/',
            icon: Events,
            title: 'Events'
        },
        {
            group: 'Dashboards',
            href: '/next/issues',
            icon: Issues,
            title: 'Issues'
        },
        {
            group: 'Dashboards',
            href: '/next/stream',
            icon: EventStream,
            title: 'Event Stream'
        },
        {
            group: 'Help',
            href: 'https://exceptionless.com/docs/',
            icon: Documentation,
            openInNewTab: true,
            title: 'Documentation'
        },
        {
            group: 'Help',
            href: 'https://github.com/exceptionless/Exceptionless/issues',
            icon: Support,
            openInNewTab: true,
            title: 'Support'
        },
        {
            group: 'Help',
            href: 'https://github.com/exceptionless/Exceptionless',
            icon: GitHub,
            openInNewTab: true,
            title: 'GitHub'
        },
        {
            group: 'Help',
            href: '/docs/index.html',
            icon: ApiDocumentations,
            openInNewTab: true,
            title: 'API'
        },
        ...accountRoutes(),
        ...eventRoutes(),
        ...projectRoutes()
    ];

    return items;
}
