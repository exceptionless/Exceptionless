import IconAccount from '~icons/mdi/account';
import IconSessions from '~icons/mdi/account-multiple';
import IconNotifications from '~icons/mdi/bell';
import IconPassword from '~icons/mdi/form-textbox-password';
import IconVerify from '~icons/mdi/shield-check';
import IconAppearance from '~icons/mdi/theme-light-dark';

import type { NavigationItem } from '../../routes';

export const routes: NavigationItem[] = [
    {
        group: 'My Account',
        href: '/next/account/manage',
        icon: IconAccount,
        title: 'Account'
    },
    {
        group: 'My Account',
        href: '/next/account/appearance',
        icon: IconAppearance,
        title: 'Appearance'
    },
    {
        group: 'My Account',
        href: '/next/account/notifications',
        icon: IconNotifications,
        show: () => false,
        title: 'Notifications'
    },
    {
        group: 'My Account',
        href: '/next/account/security',
        icon: IconPassword,
        show: () => false,
        title: 'Password and authentication'
    },
    {
        group: 'My Account',
        href: '/next/account/sessions',
        icon: IconSessions,
        show: () => false,
        title: 'Sessions'
    },
    {
        group: 'My Account',
        href: '/next/account/verify',
        icon: IconVerify,
        show: () => false,
        title: 'Verify'
    }
];
