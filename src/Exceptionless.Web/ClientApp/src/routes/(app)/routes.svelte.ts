import { resolve } from '$app/paths';
import GitHubIcon from '$comp/icons/GitHubIcon.svelte';
import { apiReferenceHref, documentationHref, githubRepositoryHref, supportIssuesHref } from '$features/shared/help-links';
import Documentation from '@lucide/svelte/icons/book-open';
import ApiDocumentations from '@lucide/svelte/icons/braces';
import Issues from '@lucide/svelte/icons/bug';
import EventStream from '@lucide/svelte/icons/calendar-arrow-down';
import Events from '@lucide/svelte/icons/calendar-days';
import Support from '@lucide/svelte/icons/circle-help';
import Sessions from '@lucide/svelte/icons/timer';

import type { NavigationItem } from '../routes.svelte';

import { routes as accountRoutes } from './account/routes.svelte';
import { routes as eventRoutes } from './event/routes.svelte';
import { routes as organizationRoutes } from './organization/routes.svelte';
import { routes as projectRoutes } from './project/routes.svelte';
import { routes as systemRoutes } from './system/routes.svelte';

export function routes(): NavigationItem[] {
    const items = [
        {
            group: 'Dashboards',
            href: resolve('/(app)'),
            icon: Events,
            title: 'Events'
        },
        {
            group: 'Dashboards',
            href: resolve('/(app)/issues'),
            icon: Issues,
            title: 'Issues'
        },
        {
            group: 'Dashboards',
            href: resolve('/(app)/stream'),
            icon: EventStream,
            title: 'Event Stream'
        },
        {
            group: 'Reports',
            href: resolve('/(app)/sessions'),
            icon: Sessions,
            title: 'Sessions'
        },
        {
            group: 'Help',
            href: documentationHref,
            icon: Documentation,
            openInNewTab: true,
            title: 'Documentation'
        },
        {
            group: 'Help',
            href: supportIssuesHref,
            icon: Support,
            openInNewTab: true,
            title: 'Support'
        },
        {
            group: 'Help',
            href: githubRepositoryHref,
            icon: GitHubIcon,
            openInNewTab: true,
            title: 'GitHub'
        },
        {
            group: 'Help',
            href: apiReferenceHref,
            icon: ApiDocumentations,
            openInNewTab: true,
            title: 'API'
        },
        ...accountRoutes(),
        ...eventRoutes(),
        ...organizationRoutes(),
        ...projectRoutes(),
        ...systemRoutes()
    ];

    return items;
}
