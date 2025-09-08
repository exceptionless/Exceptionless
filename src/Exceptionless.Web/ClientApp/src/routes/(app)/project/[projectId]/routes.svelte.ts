import { resolve } from '$app/paths';
import { page } from '$app/state';
import Usage from '@lucide/svelte/icons/bar-chart';
import ClientConfig from '@lucide/svelte/icons/braces';
import Configure from '@lucide/svelte/icons/cloud-download';
import ApiKey from '@lucide/svelte/icons/key';
import Integration from '@lucide/svelte/icons/plug-2';
import Settings from '@lucide/svelte/icons/settings';

import type { NavigationItem } from '../../../routes.svelte';

export function routes(): NavigationItem[] {
    if (!page.params.projectId) {
        return [];
    }

    return [
        {
            group: 'Project Settings',
            href: resolve('/(app)/project/[projectId]/manage', { projectId: page.params.projectId }),
            icon: Settings,
            title: 'General'
        },
        {
            group: 'Project Settings',
            href: resolve('/(app)/project/[projectId]/usage', { projectId: page.params.projectId }),
            icon: Usage,
            title: 'Usage'
        },
        {
            group: 'Project Settings',
            href: resolve('/(app)/project/[projectId]/api-keys', { projectId: page.params.projectId }),
            icon: ApiKey,
            title: 'API Keys'
        },
        {
            group: 'Project Settings',
            href: resolve('/(app)/project/[projectId]/settings', { projectId: page.params.projectId }),
            icon: Settings,
            title: 'Settings'
        },
        {
            group: 'Project Settings',
            href: resolve('/(app)/project/[projectId]/configuration-values', { projectId: page.params.projectId }),
            icon: ClientConfig,
            title: 'Configuration Values'
        },
        {
            group: 'Project Settings',
            href: resolve('/(app)/project/[projectId]/integrations', { projectId: page.params.projectId }),
            icon: Integration,
            title: 'Integrations'
        },
        {
            group: 'Project Settings',
            href: resolve('/(app)/project/[projectId]/configure', { projectId: page.params.projectId }),
            icon: Configure,
            title: 'Configure Client'
        }
    ];
}
