import BookOpen from 'lucide-svelte/icons/book-open';
import Braces from 'lucide-svelte/icons/braces';
import Stacks from 'lucide-svelte/icons/bug';
import EventStream from 'lucide-svelte/icons/calendar-arrow-down';
import Events from 'lucide-svelte/icons/calendar-days';
import Help from 'lucide-svelte/icons/circle-help';
import GitHub from 'lucide-svelte/icons/github';

import type { NavigationItem } from '../routes';

import { routes as accountRoutes } from './account/routes';

export const routes: NavigationItem[] = [
    {
        group: 'Dashboards',
        href: '/next/',
        icon: Events,
        title: 'Events'
    },
    {
        group: 'Dashboards',
        href: '/next/issues',
        icon: Stacks,
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
        icon: BookOpen,
        openInNewTab: true,
        title: 'Documentation'
    },
    {
        group: 'Help',
        href: 'https://github.com/exceptionless/Exceptionless/issues',
        icon: Help,
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
        icon: Braces,
        openInNewTab: true,
        title: 'API'
    },
    ...accountRoutes
];
