import { page } from '$app/state';
import ClientConfig from 'lucide-svelte/icons/braces';
import Configure from 'lucide-svelte/icons/cloud-download';
import ApiKey from 'lucide-svelte/icons/key';
import Integration from 'lucide-svelte/icons/plug-2';
import Settings from 'lucide-svelte/icons/settings';
import Webhook from 'lucide-svelte/icons/webhook';

import type { NavigationItem } from '../../../routes.svelte';

export function routes(): NavigationItem[] {
    if (!page.params.projectId) {
        return [];
    }

    return [
        {
            group: 'Project Settings',
            href: `/next/project/${page.params.projectId}/manage`,
            icon: Settings,
            title: 'General'
        },
        {
            group: 'Project Settings',
            href: `/next/project/${page.params.projectId}/api-keys`,
            icon: ApiKey,
            title: 'API Keys'
        },
        {
            group: 'Project Settings',
            href: `/next/project/${page.params.projectId}/settings`,
            icon: Settings,
            title: 'Settings'
        },
        {
            group: 'Project Settings',
            href: `/next/project/${page.params.projectId}/config`,
            icon: ClientConfig,
            title: 'Client Configuration'
        },
        {
            group: 'Project Settings',
            href: `/next/project/${page.params.projectId}/configure`,
            icon: Configure,
            title: 'Download & Configure Client'
        },
        {
            group: 'Project Settings',
            href: `/next/project/${page.params.projectId}/webhooks`,
            icon: Webhook,
            title: 'Webhooks'
        },
        {
            group: 'Project Settings',
            href: `/next/project/${page.params.projectId}/integrations`,
            icon: Integration,
            title: 'Integrations'
        }
    ];
}
