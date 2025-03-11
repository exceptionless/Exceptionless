import ClientConfig from 'lucide-svelte/icons/braces';
import ApiKey from 'lucide-svelte/icons/key';
import Integration from 'lucide-svelte/icons/plug-2';
import Settings from 'lucide-svelte/icons/settings';
import Webhook from 'lucide-svelte/icons/webhook';

import type { NavigationItem } from '../../routes';

export const routes: NavigationItem[] = [
    {
        group: 'Project Settings',
        href: '/next/project/[id]/manage',
        icon: Settings,
        title: 'General'
    },
    {
        group: 'Project Settings',
        href: '/next/project/[id]/manage/apikeys',
        icon: ApiKey,
        title: 'API Keys'
    },
    {
        group: 'Project Settings',
        href: '/next/project/[id]/manage/settings',
        icon: Settings,
        title: 'Settings'
    },
    {
        group: 'Project Settings',
        href: '/next/project/[id]/manage/config',
        icon: ClientConfig,
        title: 'Client Configuration'
    },
    {
        group: 'Project Settings',
        href: '/next/project/[id]/manage/webhooks',
        icon: Webhook,
        title: 'Webhooks'
    },
    {
        group: 'Project Settings',
        href: '/next/project/[id]/manage/integrations',
        icon: Integration,
        title: 'Integrations'
    }
];
