import EventStream from 'lucide-svelte/icons/calendar-arrow-down';
import Events from 'lucide-svelte/icons/calendar-days';
import Stacks from 'lucide-svelte/icons/list-checks';

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
    ...accountRoutes
];
