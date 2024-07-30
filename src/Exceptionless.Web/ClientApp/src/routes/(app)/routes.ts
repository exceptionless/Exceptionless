import IconEvents from '~icons/mdi/calendar-month-outline';
import IconStacks from '~icons/mdi/checkbox-multiple-marked-outline';
import IconEventLog from '~icons/mdi/sort-clock-descending-outline';

import type { NavigationItem } from '../routes';

import { routes as accountRoutes } from './account/routes';

export const routes: NavigationItem[] = [
    {
        group: 'Dashboards',
        href: '/next/',
        icon: IconEvents,
        title: 'Events'
    },
    {
        group: 'Dashboards',
        href: '/next/issues',
        icon: IconStacks,
        title: 'Issues'
    },
    {
        group: 'Dashboards',
        href: '/next/stream',
        icon: IconEventLog,
        title: 'Event Stream'
    },
    ...accountRoutes
];
