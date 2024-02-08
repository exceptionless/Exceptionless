import IconEvents from '~icons/mdi/calendar-month-outline';
import IconStacks from '~icons/mdi/checkbox-multiple-marked-outline';
import IconEventLog from '~icons/mdi/sort-clock-descending-outline';

import type { NavigationItem } from '../routes';
import { routes as accountRoutes } from './account/routes';

export const routes: NavigationItem[] = [
    {
        group: 'Dashboards',
        title: 'Events',
        href: '/next/',
        icon: IconEvents
    },
    {
        group: 'Dashboards',
        title: 'Issues',
        href: '/next/issues',
        icon: IconStacks
    },
    {
        group: 'Dashboards',
        title: 'Event Stream',
        href: '/next/stream',
        icon: IconEventLog
    },
    ...accountRoutes
];
