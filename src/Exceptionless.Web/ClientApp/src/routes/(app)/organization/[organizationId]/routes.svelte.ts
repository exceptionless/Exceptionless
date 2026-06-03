import { resolve } from '$app/paths';
import { page } from '$app/state';
import { organization } from '$features/organizations/context.svelte';
import Usage from '@lucide/svelte/icons/bar-chart';
import Columns from '@lucide/svelte/icons/columns-3';
import Billing from '@lucide/svelte/icons/credit-card';
import Settings from '@lucide/svelte/icons/settings';
import Users from '@lucide/svelte/icons/users';
import Zap from '@lucide/svelte/icons/zap';

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
            href: resolve('/(app)/organization/[organizationId]/custom-fields', { organizationId }),
            icon: Columns,
            title: 'Custom Fields'
        },
        {
            group: 'Organization Settings',
            href: resolve('/(app)/organization/[organizationId]/usage', { organizationId }),
            icon: Usage,
            title: 'Usage'
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
            group: 'Organization Settings',
            href: resolve('/(app)/organization/[organizationId]/features', { organizationId }),
            icon: Zap,
            show: (ctx) => !!ctx.user?.roles?.includes('global'),
            title: 'Features'
        }
    ];
}
