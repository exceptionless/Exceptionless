import { page } from '$app/state';
import { organization } from '$features/organizations/context.svelte';
import Usage from '@lucide/svelte/icons/bar-chart';
import Billing from '@lucide/svelte/icons/credit-card';
import Folder from '@lucide/svelte/icons/folder';
import Settings from '@lucide/svelte/icons/settings';
import Users from '@lucide/svelte/icons/users';

import type { NavigationItem } from '../../../routes.svelte';

export function routes(): NavigationItem[] {
    const organizationId = page.params.organizationId ?? organization.current;

    if (!organizationId) {
        return [];
    }

    return [
        {
            group: 'Organization Settings',
            href: `/next/organization/${organizationId}/manage`,
            icon: Settings,
            title: 'General'
        },
        {
            group: 'Organization Settings',
            href: `/next/organization/${organizationId}/usage`,
            icon: Usage,
            title: 'Usage'
        },
        {
            group: 'Organization Settings',
            href: `/next/organization/${organizationId}/projects`,
            icon: Folder,
            title: 'Projects'
        },
        {
            group: 'Organization Settings',
            href: `/next/organization/${organizationId}/users`,
            icon: Users,
            title: 'Users'
        },
        {
            group: 'Organization Settings',
            href: `/next/organization/${organizationId}/billing`,
            icon: Billing,
            title: 'Billing'
        },
        {
            group: 'Settings',
            href: `/next/organization/${organizationId}/projects`,
            icon: Settings,
            title: 'Projects'
        }
    ];
}
