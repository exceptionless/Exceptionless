import { page } from '$app/state';
import Events from '@lucide/svelte/icons/calendar-days';

import type { NavigationItem } from '../../routes.svelte';

export function routes(): NavigationItem[] {
    if (!page.params.eventId) {
        return [];
    }

    return [
        {
            group: 'Event',
            href: `/next/event/${page.params.eventId}`,
            icon: Events,
            show: () => false,
            title: 'Event Details'
        }
    ];
}
