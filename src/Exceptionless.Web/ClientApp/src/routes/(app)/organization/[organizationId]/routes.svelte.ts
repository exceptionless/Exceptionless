import { resolve } from '$app/paths';
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
            href: resolve('/(app)/organization/[organizationId]/manage', { organizationId }),
            icon: Settings,
            title: 'General'
        },
        {
            group: 'Organization Settings',
            href: resolve('/(app)/organization/[organizationId]/usage', { organizationId }),
            icon: Usage,
            title: 'Usage'
        },
        {
            group: 'Organization Settings',
            href: resolve('/(app)/organization/[organizationId]/projects', { organizationId }),
            icon: Folder,
            title: 'Projects'
        },
        {
            group: 'Organization Settings',
            href: resolve('/(app)/organization/[organizationId]/users', { organizationId }),
            icon: Users,
            title: 'Users'
        },
        {
            group: 'Organization Settings',
            href: resolve('/(app)/organization/[organizationId]/billing', { organizationId }),
            icon: Billing,
            title: 'Billing'
        },
        {
            group: 'Settings',
            href: resolve('/(app)/organization/[organizationId]/projects', { organizationId }),
            icon: Settings,
            title: 'Projects'
        }
    ];
}
