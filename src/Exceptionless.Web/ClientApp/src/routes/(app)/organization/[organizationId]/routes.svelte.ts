import { page } from '$app/state';
import Usage from '@lucide/svelte/icons/bar-chart';
import Billing from '@lucide/svelte/icons/credit-card';
import Settings from '@lucide/svelte/icons/settings';
import Users from '@lucide/svelte/icons/users';
import Folder from '@lucide/svelte/icons/folder';

import type { NavigationItem } from '../../../routes.svelte';

export function routes(): NavigationItem[] {
    if (!page.params.organizationId) {
        return [];
    }

    return [
        {
            group: 'Organization Settings',
            href: `/next/organization/${page.params.organizationId}/manage`,
            icon: Settings,
            title: 'General'
        },
        {
            group: 'Organization Settings',
            href: `/next/organization/${page.params.organizationId}/usage`,
            icon: Usage,
            title: 'Usage'
        },
        {
            group: 'Organization Settings',
            href: `/next/organization/${page.params.organizationId}/projects`,
            icon: Folder,
            title: 'Projects'
        },
        {
            group: 'Organization Settings',
            href: `/next/organization/${page.params.organizationId}/users`,
            icon: Users,
            title: 'Users'
        },
        {
            group: 'Organization Settings',
            href: `/next/organization/${page.params.organizationId}/billing`,
            icon: Billing,
            title: 'Billing'
        }
    ];
}
