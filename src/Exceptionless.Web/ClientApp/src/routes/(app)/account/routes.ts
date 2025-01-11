import Notifications from 'lucide-svelte/icons/bell';
import Password from 'lucide-svelte/icons/key-round';
import Verify from 'lucide-svelte/icons/shield-check';
import Appearance from 'lucide-svelte/icons/sun-moon';
import Account from 'lucide-svelte/icons/user';
import Sessions from 'lucide-svelte/icons/users';

import type { NavigationItem } from '../../routes';

export const routes: NavigationItem[] = [
    {
        group: 'My Account',
        href: '/next/account/manage',
        icon: Account,
        title: 'Account'
    },
    {
        group: 'My Account',
        href: '/next/account/appearance',
        icon: Appearance,
        title: 'Appearance'
    },
    {
        group: 'My Account',
        href: '/next/account/notifications',
        icon: Notifications,
        show: () => false,
        title: 'Notifications'
    },
    {
        group: 'My Account',
        href: '/next/account/security',
        icon: Password,
        show: () => false,
        title: 'Password and authentication'
    },
    {
        group: 'My Account',
        href: '/next/account/sessions',
        icon: Sessions,
        show: () => false,
        title: 'Sessions'
    },
    {
        group: 'My Account',
        href: '/next/account/verify',
        icon: Verify,
        show: () => false,
        title: 'Verify'
    }
];
